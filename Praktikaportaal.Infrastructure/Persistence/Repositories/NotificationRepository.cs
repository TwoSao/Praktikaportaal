using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly PraktikaportaalDbContext _context;

    public NotificationRepository(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetByUserAsync(
        Guid userId, NotificationQueryParameters query, CancellationToken ct = default)
    {
        var source = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.UnreadOnly)
            source = source.Where(n => !n.IsRead);

        int total = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(n => n.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread)
            n.IsRead = true;
        return unread.Count; // saved by the unit of work
    }

    public async Task AddAsync(Notification notification, CancellationToken ct = default) =>
        await _context.Notifications.AddAsync(notification, ct);
}
