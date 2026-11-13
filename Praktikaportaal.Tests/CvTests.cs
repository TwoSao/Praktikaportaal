using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;
using Praktikaportaal.Core.Services;
using Praktikaportaal.Infrastructure.Options;
using Praktikaportaal.Infrastructure.Services;
using Xunit;

namespace Praktikaportaal.Tests;

public class CvFileRulesTests
{
    private const string Pdf = "application/pdf";
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Theory]
    [InlineData("cv.pdf", Pdf, ".pdf")]
    [InlineData("CV.PDF", Pdf, ".pdf")]
    [InlineData("cv.docx", Docx, ".docx")]
    [InlineData("cv.pdf", "application/octet-stream", ".pdf")]   // some browsers
    [InlineData("cv.pdf", "application/pdf; charset=binary", ".pdf")]
    [InlineData("cv.pdf", null, ".pdf")]
    public void Validate_AcceptsPdfAndDocx(string name, string? contentType, string expectedExtension) =>
        Assert.Equal(expectedExtension, CvFileRules.Validate(name, contentType, 1000).Extension);

    [Theory]
    [InlineData("cv.exe", "application/octet-stream")]
    [InlineData("cv.doc", "application/msword")]
    [InlineData("cv.pdf.exe", Pdf)]
    [InlineData("cv", Pdf)]
    [InlineData("cv.txt", "text/plain")]
    public void Validate_RejectsOtherTypes(string name, string contentType) =>
        Assert.Throws<BusinessRuleException>(() => CvFileRules.Validate(name, contentType, 1000));

    [Fact]
    public void Validate_RejectsContentTypeThatDoesNotMatchExtension() =>
        Assert.Throws<BusinessRuleException>(() => CvFileRules.Validate("cv.pdf", "text/html", 1000));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CvFileRules.MaxSizeBytes + 1)]
    public void Validate_RejectsEmptyAndOversizedFiles(long length) =>
        Assert.Throws<BusinessRuleException>(() => CvFileRules.Validate("cv.pdf", Pdf, length));

    [Fact]
    public void Validate_AcceptsExactlyMaxSize() =>
        Assert.Equal(".pdf", CvFileRules.Validate("cv.pdf", Pdf, CvFileRules.MaxSizeBytes).Extension);

    [Fact]
    public void Signature_MustMatchTheFormat()
    {
        var pdf = CvFileRules.Validate("a.pdf", Pdf, 10);
        var docx = CvFileRules.Validate("a.docx", Docx, 10);

        Assert.True(CvFileRules.HasValidSignature(pdf, "%PDF-1.7"u8));
        Assert.True(CvFileRules.HasValidSignature(docx, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0 }));
        Assert.False(CvFileRules.HasValidSignature(pdf, "MZ\x90\0"u8));          // Windows executable renamed to .pdf
        Assert.False(CvFileRules.HasValidSignature(pdf, new byte[] { 0x50, 0x4B, 0x03, 0x04 })); // zip as pdf
        Assert.False(CvFileRules.HasValidSignature(docx, "%PDF-1.7"u8));
    }

    [Theory]
    [InlineData(@"C:\Users\x\Documents\My CV.pdf", "My CV.pdf")]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("bad<>|name?.pdf", "badname.pdf")]
    [InlineData(".pdf", "cv.pdf")]
    public void SanitizeFileName_RemovesPathsAndUnsafeCharacters(string input, string expected) =>
        Assert.Equal(expected, CvFileRules.SanitizeFileName(input, new CvFileType(".pdf", Pdf)));

    [Fact]
    public void SanitizeFileName_LimitsLength() =>
        Assert.True(CvFileRules.SanitizeFileName(new string('a', 400) + ".pdf", new CvFileType(".pdf", Pdf)).Length <= 155);
}

public class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "praktikaportaal-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SaveReadDelete_RoundTrip()
    {
        string key = await _storage.SaveAsync(new MemoryStream("hello"u8.ToArray()), ".pdf");

        Assert.EndsWith(".pdf", key);
        using (var read = _storage.OpenRead(key))
            Assert.Equal("hello", await new StreamReader(read, Encoding.UTF8).ReadToEndAsync());

        await _storage.DeleteAsync(key);
        Assert.Throws<FileNotFoundException>(() => _storage.OpenRead(key));
        await _storage.DeleteAsync(key); // deleting twice is fine
    }

    [Fact]
    public async Task Save_UsesRandomKeys()
    {
        string a = await _storage.SaveAsync(new MemoryStream([1]), ".pdf");
        string b = await _storage.SaveAsync(new MemoryStream([1]), ".pdf");

        Assert.NotEqual(a, b);
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Theory]
    [InlineData("../secret.pdf")]
    [InlineData("..\\secret.pdf")]
    [InlineData("subdir/file.pdf")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("not-a-valid-key.pdf")]
    public void OpenRead_RejectsKeysThatCouldEscapeTheStorageRoot(string key) =>
        Assert.Throws<ArgumentException>(() => _storage.OpenRead(key));

    [Theory]
    [InlineData("pdf")]
    [InlineData(".pdf/../../x")]
    [InlineData(".")]
    public async Task Save_RejectsInvalidExtensions(string extension) =>
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(new MemoryStream([1]), extension));
}

/// <summary>CvService behaviour with mocked repositories and storage.</summary>
public class CvServiceTests
{
    private readonly Mock<ICvRepository> _cvs = new();
    private readonly Mock<IStudentProfileRepository> _students = new();
    private readonly Mock<IApplicationRepository> _applications = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    public CvServiceTests()
    {
        _students.Setup(r => r.GetByIdAsync(_studentId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new StudentProfile { User = new User() });
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("new-key.pdf");
    }

    private CvService CreateService() =>
        new(_cvs.Object, _students.Object, _applications.Object, _storage.Object, _unitOfWork.Object);

    private static MemoryStream PdfBytes() => new("%PDF-1.7 fake pdf body"u8.ToArray());

    private void StoredCvs(params CV[] cvs) =>
        _cvs.Setup(r => r.GetByStudentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(cvs);

    [Fact]
    public async Task Upload_ValidPdf_StoresFileAndSavesMetadata()
    {
        StoredCvs();
        var pdf = PdfBytes();

        var dto = await CreateService().UploadAsync(_studentId, "My CV.pdf", "application/pdf", pdf.Length, pdf);

        Assert.Equal("My CV.pdf", dto.FileName);
        Assert.Equal("application/pdf", dto.ContentType);
        _cvs.Verify(r => r.AddAsync(It.Is<CV>(c => c.StorageKey == "new-key.pdf" && c.StudentProfileId == _studentId), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_FakePdfWithWrongContent_IsRejectedBeforeStoring()
    {
        StoredCvs();
        var notPdf = new MemoryStream("MZ this is an executable"u8.ToArray());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().UploadAsync(_studentId, "cv.pdf", "application/pdf", notPdf.Length, notPdf));

        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_Replacement_DeletesOldFileAndMarksOldRecordDeleted()
    {
        var old = new CV { StorageKey = "old-key.pdf", StudentProfileId = _studentId };
        StoredCvs(old);
        var pdf = PdfBytes();

        await CreateService().UploadAsync(_studentId, "cv.pdf", "application/pdf", pdf.Length, pdf);

        Assert.True(old.IsDeleted);
        _storage.Verify(s => s.DeleteAsync("old-key.pdf", It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.DeleteAsync("new-key.pdf", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_WhenSaveFails_RemovesTheNewFile()
    {
        StoredCvs();
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));
        var pdf = PdfBytes();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().UploadAsync(_studentId, "cv.pdf", "application/pdf", pdf.Length, pdf));

        _storage.Verify(s => s.DeleteAsync("new-key.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    private Application SetupApplication(ApplicationStatus status = ApplicationStatus.Submitted)
    {
        var application = new Application
        {
            StudentProfileId = _studentId,
            Status = status,
            Internship = new Internship { CompanyProfileId = _companyId }
        };
        _applications.Setup(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(application);
        _storage.Setup(s => s.OpenRead("cv-key.pdf")).Returns(new MemoryStream([1, 2, 3]));
        StoredCvs(new CV { StorageKey = "cv-key.pdf", FileName = "cv.pdf", ContentType = "application/pdf", StudentProfileId = _studentId });
        return application;
    }

    [Fact]
    public async Task DownloadForApplication_OwningCompany_GetsFile()
    {
        var application = SetupApplication();
        var owner = new CurrentUser(Guid.NewGuid(), UserRole.Company, _companyId);

        var file = await CreateService().DownloadForApplicationAsync(application.Id, owner);
        file.Content.Dispose();

        Assert.Equal("cv.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
    }

    [Fact]
    public async Task DownloadForApplication_OtherCompany_ThrowsForbidden()
    {
        var application = SetupApplication();
        var other = new CurrentUser(Guid.NewGuid(), UserRole.Company, Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService().DownloadForApplicationAsync(application.Id, other));
        _storage.Verify(s => s.OpenRead(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DownloadForApplication_Withdrawn_ThrowsForbidden()
    {
        var application = SetupApplication(ApplicationStatus.Withdrawn);
        var owner = new CurrentUser(Guid.NewGuid(), UserRole.Company, _companyId);

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService().DownloadForApplicationAsync(application.Id, owner));
    }

    [Fact]
    public async Task DownloadMyCv_WithoutUpload_ThrowsNotFound()
    {
        StoredCvs();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().DownloadMyCvAsync(_studentId));
    }

    [Fact]
    public async Task DownloadMyCv_WhenFileMissingOnDisk_ThrowsNotFound()
    {
        StoredCvs(new CV { StorageKey = "gone.pdf", FileName = "cv.pdf", ContentType = "application/pdf" });
        _storage.Setup(s => s.OpenRead("gone.pdf")).Throws<FileNotFoundException>();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().DownloadMyCvAsync(_studentId));
    }
}
