using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Services;

/// <summary>
/// Allowed application status transitions, enforced in the services layer.
/// <code>
/// Submitted ─► Viewed ─► Interview ─► Accepted
///     │           │          │
///     └───────────┴──────────┴──────► Rejected
/// Submitted / Viewed / Interview ──► Withdrawn   (student only)
/// </code>
/// Accepted, Rejected and Withdrawn are final.
/// </summary>
public static class ApplicationStatusRules
{
    private static readonly IReadOnlyDictionary<ApplicationStatus, ApplicationStatus[]> CompanyTransitions =
        new Dictionary<ApplicationStatus, ApplicationStatus[]>
        {
            [ApplicationStatus.Submitted] = [ApplicationStatus.Viewed, ApplicationStatus.Rejected],
            [ApplicationStatus.Viewed] = [ApplicationStatus.Interview, ApplicationStatus.Rejected],
            [ApplicationStatus.Interview] = [ApplicationStatus.Accepted, ApplicationStatus.Rejected],
            [ApplicationStatus.Accepted] = [],
            [ApplicationStatus.Rejected] = [],
            [ApplicationStatus.Withdrawn] = []
        };

    /// <summary>Statuses a company may move an application to from <paramref name="current"/>.</summary>
    public static IReadOnlyList<ApplicationStatus> AllowedForCompany(ApplicationStatus current) =>
        CompanyTransitions[current];

    public static bool CanCompanyChange(ApplicationStatus current, ApplicationStatus next) =>
        CompanyTransitions[current].Contains(next);

    /// <summary>A student may withdraw only while the application is still open.</summary>
    public static bool CanStudentWithdraw(ApplicationStatus current) =>
        current is ApplicationStatus.Submitted or ApplicationStatus.Viewed or ApplicationStatus.Interview;
}
