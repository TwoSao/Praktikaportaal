using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="StudentProfile"/>. Returned profiles include the user.</summary>
public interface IStudentProfileRepository
{
    Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
