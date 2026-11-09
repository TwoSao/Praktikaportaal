using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class StudentProfileRepository : IStudentProfileRepository
{
    private readonly PraktikaportaalDbContext _context;

    public StudentProfileRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.StudentProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id, ct);
}
