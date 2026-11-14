using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Interfaces;

/// <summary>
/// CV upload and download. A student has one current CV: uploading a new one replaces the old file.
/// </summary>
public interface ICvService
{
    /// <summary>Validates (type, size, signature) and stores the CV, replacing the previous one.</summary>
    Task<CvDto> UploadAsync(Guid studentProfileId, string? fileName, string? contentType, long length, Stream content, CancellationToken ct = default);

    Task<CvDto> GetMyCvAsync(Guid studentProfileId, CancellationToken ct = default);

    Task<CvFile> DownloadMyCvAsync(Guid studentProfileId, CancellationToken ct = default);

    Task DeleteMyCvAsync(Guid studentProfileId, CancellationToken ct = default);

    /// <summary>
    /// CV of the student behind an application. Allowed for the company that owns the internship
    /// (while the application is not withdrawn) and for admins.
    /// </summary>
    Task<CvFile> DownloadForApplicationAsync(Guid applicationId, CurrentUser caller, CancellationToken ct = default);
}
