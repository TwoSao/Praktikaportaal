namespace Praktikaportaal.Core.Services;

/// <summary>
/// Skill overlap between a student and an internship:
/// <c>(student skills ∩ required skills) / required skills × 100</c>.
/// Pure and side-effect free, so it is easy to unit test.
/// </summary>
public static class RecommendationCalculator
{
    /// <summary>
    /// Overlap percentage, 0–100, rounded to one decimal.
    /// Returns 0 when the internship has no required skills (nothing to match against).
    /// </summary>
    public static double OverlapPercent(IReadOnlySet<Guid> studentSkillIds, IReadOnlyCollection<Guid> requiredSkillIds)
    {
        var required = requiredSkillIds.Distinct().ToList();
        if (required.Count == 0)
            return 0;

        int matched = required.Count(studentSkillIds.Contains);
        return Math.Round(matched * 100.0 / required.Count, 1, MidpointRounding.AwayFromZero);
    }
}
