using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PraktikaportaalDbContext _context;

    public UserRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    private IQueryable<User> WithProfiles() => _context.Users
        .Include(u => u.StudentProfile)
        .Include(u => u.CompanyProfile);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        WithProfiles().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        WithProfiles().FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default) =>
        _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await _context.Users.AddAsync(user, ct);
}
