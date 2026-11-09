using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="User"/>. Returned users include their student/company profile.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
