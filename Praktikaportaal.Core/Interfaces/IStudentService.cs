using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Interfaces;

/// <summary>Student profile and skills.</summary>
public interface IStudentService
{
    /// <summary>The student's own profile with skills.</summary>
    Task<StudentProfileDto> GetProfileAsync(Guid studentProfileId, CancellationToken ct = default);

    /// <summary>Replaces profile fields and the whole skill list.</summary>
    Task<StudentProfileDto> UpdateProfileAsync(Guid studentProfileId, UpdateStudentProfileDto dto, CancellationToken ct = default);

    /// <summary>All students, paged (Service account for the Worker, Admin).</summary>
    Task<PagedResult<StudentProfileDto>> GetStudentsAsync(PagingParameters paging, CancellationToken ct = default);
}
