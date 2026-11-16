using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.API.Extensions;
using Praktikaportaal.Core.Constants;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Creates a notification for a user. Used by the Worker (Service role) to deliver the daily
    /// internship digest; Admin may also use it.
    /// </summary>
    [Authorize(Roles = Roles.ServiceOrAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> Create([FromBody] CreateNotificationDto dto, CancellationToken ct)
    {
        var created = await _notificationService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetMy), null, created);
    }

    /// <summary>The logged-in user's notifications, newest first, with the unread count.</summary>
    [Authorize]
    [HttpGet("my")]
    [ProducesResponseType(typeof(NotificationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationListDto>> GetMy([FromQuery] NotificationQueryParameters query, CancellationToken ct) =>
        Ok(await _notificationService.GetMyNotificationsAsync(User.GetUserId(), query, ct));

    /// <summary>Marks one of the logged-in user's notifications as read.</summary>
    [Authorize]
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _notificationService.MarkReadAsync(id, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Marks all of the logged-in user's notifications as read.</summary>
    [Authorize]
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _notificationService.MarkAllReadAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
