using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces.Repositories;
using Praktikaportaal.Core.Services;
using Xunit;

namespace Praktikaportaal.Tests;

/// <summary>
/// InternshipService ownership / moderation rules, tested with mocked repositories (Moq).
/// </summary>
public class InternshipServiceTests
{
    private readonly Mock<IInternshipRepository> _internships = new();
    private readonly Mock<ISkillRepository> _skills = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Guid _ownerCompanyId = Guid.NewGuid();

    private InternshipService CreateService() => new(_internships.Object, _skills.Object, _unitOfWork.Object);

    private Internship SetupInternship(InternshipStatus status = InternshipStatus.Published)
    {
        var internship = new Internship
        {
            CompanyProfileId = _ownerCompanyId,
            CompanyProfile = new CompanyProfile { CompanyName = "Owner OÜ" },
            Title = "Intern",
            Description = "Description",
            Field = "IT",
            Status = status
        };
        _internships.Setup(r => r.GetByIdAsync(internship.Id, It.IsAny<CancellationToken>())).ReturnsAsync(internship);
        return internship;
    }

    [Fact]
    public async Task Delete_ByOtherCompany_ThrowsForbidden_AndDoesNotSave()
    {
        var internship = SetupInternship();
        var otherCompany = new CurrentUser(Guid.NewGuid(), UserRole.Company, Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService().DeleteInternshipAsync(internship.Id, otherCompany));

        Assert.False(internship.IsDeleted);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ByAdmin_SoftDeletesAndSaves()
    {
        var internship = SetupInternship();
        var admin = new CurrentUser(Guid.NewGuid(), UserRole.Admin, null);

        await CreateService().DeleteInternshipAsync(internship.Id, admin);

        Assert.True(internship.IsDeleted);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_ByOwningCompany_ChangesStatus()
    {
        var internship = SetupInternship();
        var owner = new CurrentUser(Guid.NewGuid(), UserRole.Company, _ownerCompanyId);

        await CreateService().UpdateInternshipStatusAsync(internship.Id, InternshipStatus.Closed, owner);

        Assert.Equal(InternshipStatus.Closed, internship.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_DraftForAnonymous_ThrowsNotFound_ButOwnerSeesIt()
    {
        var internship = SetupInternship(InternshipStatus.Draft);
        var owner = new CurrentUser(Guid.NewGuid(), UserRole.Company, _ownerCompanyId);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetInternshipByIdAsync(internship.Id, caller: null));

        var dto = await CreateService().GetInternshipByIdAsync(internship.Id, owner);
        Assert.Equal(InternshipStatus.Draft, dto.Status);
    }

    [Fact]
    public async Task Create_WithUnknownSkillId_ThrowsBusinessRule()
    {
        _skills.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

        var dto = new CreateInternshipDto
        {
            Title = "Intern",
            Description = "Long enough description",
            Field = "IT",
            SkillIds = [Guid.NewGuid()]
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateInternshipAsync(_ownerCompanyId, dto));
        _internships.Verify(r => r.AddAsync(It.IsAny<Internship>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
