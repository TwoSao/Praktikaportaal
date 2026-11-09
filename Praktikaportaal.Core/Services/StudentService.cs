using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class StudentService : IStudentService
{
    private readonly IStudentProfileRepository _students;
    private readonly ISkillRepository _skills;
    private readonly IUnitOfWork _unitOfWork;

    public StudentService(IStudentProfileRepository students, ISkillRepository skills, IUnitOfWork unitOfWork)
    {
        _students = students;
        _skills = skills;
        _unitOfWork = unitOfWork;
    }

    public async Task<StudentProfileDto> GetProfileAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var student = await _students.GetWithSkillsAsync(studentProfileId, ct)
            ?? throw new NotFoundException("Student profile", studentProfileId);
        return MapToDto(student);
    }

    public async Task<StudentProfileDto> UpdateProfileAsync(
        Guid studentProfileId,
        UpdateStudentProfileDto dto,
        CancellationToken ct = default)
    {
        var student = await _students.GetWithSkillsAsync(studentProfileId, ct)
            ?? throw new NotFoundException("Student profile", studentProfileId);

        var duplicate = dto.Skills.GroupBy(s => s.SkillId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
            throw new BusinessRuleException($"Skill {duplicate.Key} is listed more than once.");

        var known = await _skills.GetByIdsAsync(dto.Skills.Select(s => s.SkillId), ct);
        var unknown = dto.Skills.Select(s => s.SkillId).Except(known.Select(s => s.Id)).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException($"Unknown skill id(s): {string.Join(", ", unknown)}.");

        student.User.FirstName = dto.FirstName.Trim();
        student.User.LastName = dto.LastName.Trim();
        student.SchoolName = dto.SchoolName.Trim();
        student.StudyProgram = dto.StudyProgram.Trim();
        student.Course = dto.Course;
        student.Description = dto.Description?.Trim();
        student.PhoneNumber = dto.PhoneNumber?.Trim();
        student.Location = dto.Location?.Trim();

        // Replace the skill list: drop removed, update levels of kept, add new.
        var wanted = dto.Skills.ToDictionary(s => s.SkillId, s => s.Level);
        foreach (var removed in student.StudentSkills.Where(s => !wanted.ContainsKey(s.SkillId)).ToList())
            student.StudentSkills.Remove(removed);

        foreach (var (skillId, level) in wanted)
        {
            var existing = student.StudentSkills.FirstOrDefault(s => s.SkillId == skillId);
            if (existing != null)
                existing.Level = level;
            else
                student.StudentSkills.Add(new StudentSkill { StudentProfileId = student.Id, SkillId = skillId, Level = level });
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Reload so the response contains skill names.
        return await GetProfileAsync(studentProfileId, ct);
    }

    public async Task<PagedResult<StudentProfileDto>> GetStudentsAsync(PagingParameters paging, CancellationToken ct = default)
    {
        var (items, total) = await _students.GetPagedAsync(paging, ct);

        return new PagedResult<StudentProfileDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = paging.Page,
            PageSize = paging.PageSize
        };
    }

    private static StudentProfileDto MapToDto(StudentProfile s) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        Email = s.User.Email,
        FirstName = s.User.FirstName,
        LastName = s.User.LastName,
        SchoolName = s.SchoolName,
        StudyProgram = s.StudyProgram,
        Course = s.Course,
        Description = s.Description,
        PhoneNumber = s.PhoneNumber,
        Location = s.Location,
        HasCv = s.CVs.Count > 0,
        Skills = s.StudentSkills
            .Where(ss => ss.Skill != null)
            .OrderBy(ss => ss.Skill.Name)
            .Select(ss => new StudentSkillDto { SkillId = ss.SkillId, Name = ss.Skill.Name, Level = ss.Level })
            .ToList()
    };
}
