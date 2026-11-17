using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Core.Interfaces;

public interface INotificationService
{
    /// <summary>Creates a notification for an active user (used by the Worker's daily digest).</summary>
    Task<NotificationDto> CreateAsync(CreateNotificationDto dto, CancellationToken ct = default);

    /// <summary>The user's own notifications, newest first, with the unread count.</summary>
    Task<NotificationListDto> GetMyNotificationsAsync(Guid userId, NotificationQueryParameters query, CancellationToken ct = default);

    /// <summary>Marks one of the user's own notifications as read.</summary>
    Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);

    /// <summary>Marks all of the user's notifications as read; returns how many changed.</summary>
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
