using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class RecommendationService : IRecommendationService
{
    private readonly IStudentProfileRepository _students;
    private readonly IInternshipRepository _internships;
    private readonly IApplicationRepository _applications;

    public RecommendationService(
        IStudentProfileRepository students,
        IInternshipRepository internships,
        IApplicationRepository applications)
    {
        _students = students;
        _internships = internships;
        _applications = applications;
    }

    public async Task<PagedResult<RecommendationDto>> GetRecommendationsAsync(
        Guid studentProfileId,
        RecommendationQueryParameters query,
        CurrentUser caller,
        CancellationToken ct = default)
    {
        bool allowed = caller.Role is UserRole.Admin or UserRole.Service ||
                       (caller.Role == UserRole.Student && caller.ProfileId == studentProfileId);
        if (!allowed)
            throw new ForbiddenException("You can only see your own recommendations.");

        var student = await _students.GetWithSkillsAsync(studentProfileId, ct)
            ?? throw new NotFoundException("Student profile", studentProfileId);

        var studentSkillIds = student.StudentSkills.Select(s => s.SkillId).ToHashSet();
        var alreadyApplied = (await _applications.GetActiveInternshipIdsAsync(studentProfileId, ct)).ToHashSet();
        var open = await _internships.GetOpenAsync(query.Since, ct);

        // Overlap is computed here (services layer), not in SQL: the number of open internships is small
        // and the rule stays readable and unit-testable.
        var ranked = open
            .Where(i => !alreadyApplied.Contains(i.Id))
            .Select(i => Score(i, studentSkillIds))
            .Where(r => r.OverlapPercent >= query.MinOverlap && r.OverlapPercent > 0)
            .OrderByDescending(r => r.OverlapPercent)
            .ThenByDescending(r => r.MatchedSkills.Count)
            .ThenByDescending(r => r.Internship.CreatedAt)
            .ToList();

        return new PagedResult<RecommendationDto>
        {
            Items = ranked.Skip(query.Skip).Take(query.PageSize).ToList(),
            TotalCount = ranked.Count,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    private static RecommendationDto Score(Entities.Internship internship, IReadOnlySet<Guid> studentSkillIds)
    {
        var required = internship.InternshipSkills.Where(s => s.Skill != null).ToList();

        return new RecommendationDto
        {
            Internship = InternshipMapper.ToDto(internship),
            OverlapPercent = RecommendationCalculator.OverlapPercent(studentSkillIds, required.Select(s => s.SkillId).ToList()),
            MatchedSkills = required.Where(s => studentSkillIds.Contains(s.SkillId)).Select(s => s.Skill.Name).Order().ToList(),
            MissingSkills = required.Where(s => !studentSkillIds.Contains(s.SkillId)).Select(s => s.Skill.Name).Order().ToList()
        };
    }
}
