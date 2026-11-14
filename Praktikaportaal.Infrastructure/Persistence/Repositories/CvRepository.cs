using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class CvRepository : ICvRepository
{
    private readonly PraktikaportaalDbContext _context;

    public CvRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CV>> GetByStudentAsync(Guid studentProfileId, CancellationToken ct = default) =>
        await _context.CVs
            .Where(cv => cv.StudentProfileId == studentProfileId)
            .OrderByDescending(cv => cv.UploadedAt)
            .ToListAsync(ct);

    public async Task AddAsync(CV cv, CancellationToken ct = default) =>
        await _context.CVs.AddAsync(cv, ct);
}
