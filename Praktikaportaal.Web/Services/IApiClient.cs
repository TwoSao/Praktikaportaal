using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Web.Services;

/// <summary>A file downloaded from the API (CV).</summary>
public record ApiFile(byte[] Content, string FileName, string ContentType);

/// <summary>
/// The MVC site's only way to reach the system: the REST API. Every method uses the JWT of the logged-in
/// user (added by <see cref="BearerTokenHandler"/>) and throws <see cref="ApiException"/> for API errors.
/// </summary>
public interface IApiClient
{
    // ── auth ────────────────────────────────────────────────────────────────
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<AuthResultDto> RegisterStudentAsync(RegisterStudentDto dto, CancellationToken ct = default);
    Task<AuthResultDto> RegisterCompanyAsync(RegisterCompanyDto dto, CancellationToken ct = default);

    // ── internships ─────────────────────────────────────────────────────────
    Task<PagedResult<InternshipDto>> SearchInternshipsAsync(InternshipQueryParameters query, CancellationToken ct = default);
    Task<InternshipFiltersDto> GetInternshipFiltersAsync(CancellationToken ct = default);
    Task<InternshipDto> GetInternshipAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<InternshipDto>> GetMyInternshipsAsync(CancellationToken ct = default);
    Task<InternshipDto> CreateInternshipAsync(CreateInternshipDto dto, CancellationToken ct = default);
    Task<InternshipDto> UpdateInternshipAsync(Guid id, UpdateInternshipDto dto, CancellationToken ct = default);
    Task DeleteInternshipAsync(Guid id, CancellationToken ct = default);
    Task SetInternshipStatusAsync(Guid id, InternshipStatus status, CancellationToken ct = default);

    // ── applications ────────────────────────────────────────────────────────
    Task<ApplicationDto> ApplyAsync(Guid internshipId, CreateApplicationDto dto, CancellationToken ct = default);
    Task<PagedResult<ApplicationDto>> GetMyApplicationsAsync(ApplicationQueryParameters query, CancellationToken ct = default);
    Task<PagedResult<ApplicationDto>> GetInternshipApplicationsAsync(Guid internshipId, ApplicationQueryParameters query, CancellationToken ct = default);
    Task SetApplicationStatusAsync(Guid applicationId, ApplicationStatus status, CancellationToken ct = default);
    Task WithdrawApplicationAsync(Guid applicationId, CancellationToken ct = default);
    Task<ApiFile> DownloadApplicantCvAsync(Guid applicationId, CancellationToken ct = default);

    // ── student profile, CV, recommendations ────────────────────────────────
    Task<StudentProfileDto> GetMyProfileAsync(CancellationToken ct = default);
    Task<StudentProfileDto> UpdateMyProfileAsync(UpdateStudentProfileDto dto, CancellationToken ct = default);
    Task<CvDto?> GetMyCvAsync(CancellationToken ct = default);
    Task<CvDto> UploadMyCvAsync(Stream content, string fileName, string? contentType, CancellationToken ct = default);
    Task DeleteMyCvAsync(CancellationToken ct = default);
    Task<ApiFile> DownloadMyCvAsync(CancellationToken ct = default);
    Task<PagedResult<RecommendationDto>> GetRecommendationsAsync(Guid studentProfileId, RecommendationQueryParameters query, CancellationToken ct = default);

    // ── skills ──────────────────────────────────────────────────────────────
    Task<IReadOnlyList<SkillDto>> GetSkillsAsync(CancellationToken ct = default);

    // ── notifications ───────────────────────────────────────────────────────
    Task<NotificationListDto> GetNotificationsAsync(NotificationQueryParameters query, CancellationToken ct = default);
    Task MarkNotificationReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllNotificationsReadAsync(CancellationToken ct = default);

    // ── admin ───────────────────────────────────────────────────────────────
    Task<ApplicationStatisticsDto> GetStatisticsAsync(StatisticsQueryParameters query, CancellationToken ct = default);
}
