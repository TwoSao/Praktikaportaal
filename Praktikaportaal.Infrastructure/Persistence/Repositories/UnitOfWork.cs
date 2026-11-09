using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly PraktikaportaalDbContext _context;

    public UnitOfWork(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
