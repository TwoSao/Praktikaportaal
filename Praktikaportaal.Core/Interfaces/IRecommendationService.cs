using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Interfaces;

/// <summary>Internship recommendations by skill overlap.</summary>
public interface IRecommendationService
{
    /// <summary>
    /// Open internships ranked by overlap (highest first) for a student. Excludes internships the student has
    /// already applied to, closed ones and ones past their deadline.
    /// Allowed for the student themselves, the Service account and admins.
    /// </summary>
    Task<PagedResult<RecommendationDto>> GetRecommendationsAsync(
        Guid studentProfileId, RecommendationQueryParameters query, CurrentUser caller, CancellationToken ct = default);
}
