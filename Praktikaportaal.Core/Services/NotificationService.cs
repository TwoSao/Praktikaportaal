using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(INotificationRepository notifications, IUserRepository users, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    public async Task<NotificationDto> CreateAsync(CreateNotificationDto dto, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(dto.UserId, ct)
            ?? throw new NotFoundException("User", dto.UserId);

        if (!user.IsActive)
            throw new BusinessRuleException("Cannot notify a deactivated user.");

        var notification = new Notification
        {
            UserId = user.Id,
            Title = dto.Title.Trim(),
            Message = dto.Message.Trim()
        };

        await _notifications.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(notification);
    }

    public async Task<NotificationListDto> GetMyNotificationsAsync(
        Guid userId, NotificationQueryParameters query, CancellationToken ct = default)
    {
        var (items, total) = await _notifications.GetByUserAsync(userId, query, ct);

        return new NotificationListDto
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
            UnreadCount = await _notifications.CountUnreadAsync(userId, ct)
        };
    }

    public async Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, ct);

        // Someone else's notification looks like a missing one: don't reveal that it exists.
        if (notification == null || notification.UserId != userId)
            throw new NotFoundException("Notification", notificationId);

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        int changed = await _notifications.MarkAllReadAsync(userId, ct);
        if (changed > 0)
            await _unitOfWork.SaveChangesAsync(ct);
        return changed;
    }

    private static NotificationDto MapToDto(Notification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Message = n.Message,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt
    };
}
