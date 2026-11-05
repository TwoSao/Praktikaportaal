namespace Praktikaportaal.Core.Interfaces.Repositories;

/// <summary>
/// Commits all changes tracked by the repositories in the current scope as one transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
