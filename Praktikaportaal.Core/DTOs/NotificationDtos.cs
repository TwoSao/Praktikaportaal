using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Core.DTOs;

/// <summary>Creates a notification for a user (Service account / Admin).</summary>
public class CreateNotificationDto
{
    /// <summary>Recipient user id (the "userId" of a student from GET /api/students).</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <example>3 new internships match your skills</example>
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    /// <example>Junior .NET Developer Intern (100 %), DevOps Intern (50 %)</example>
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

public class NotificationDto
{
    public Guid Id { get; set; }

    /// <example>Application status changed</example>
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Query string of GET /api/notifications/my.</summary>
public class NotificationQueryParameters : PagingParameters
{
    /// <summary>Only unread notifications.</summary>
    public bool UnreadOnly { get; set; }
}

/// <summary>A page of notifications plus the total number of unread ones (for the badge in the UI).</summary>
public class NotificationListDto : PagedResult<NotificationDto>
{
    /// <example>2</example>
    public int UnreadCount { get; set; }
}
