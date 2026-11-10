using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces.Repositories;

public interface INotificationRepository
{
    /// <summary>The user's notifications, newest first, one page + total count (read-only).</summary>
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetByUserAsync(
        Guid userId, NotificationQueryParameters query, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tracked, so it can be marked as read.</summary>
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Marks all unread notifications of the user as read and returns how many were changed.</summary>
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Notification notification, CancellationToken ct = default);
}
