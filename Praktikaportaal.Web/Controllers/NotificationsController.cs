using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

/// <summary>The logged-in user's notifications (every role).</summary>
[Authorize]
public class NotificationsController : Controller
{
    private const int PageSize = 15;

    private readonly IApiClient _api;

    public NotificationsController(IApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool unreadOnly = false, int page = 1)
    {
        var result = await _api.GetNotificationsAsync(new NotificationQueryParameters
        {
            UnreadOnly = unreadOnly, Page = Math.Max(1, page), PageSize = PageSize
        });
        return View(new NotificationsViewModel { Result = result, UnreadOnly = unreadOnly });
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(Guid id, bool unreadOnly = false, int page = 1)
    {
        await _api.MarkNotificationReadAsync(id);
        return RedirectToAction(nameof(Index), new { unreadOnly, page });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        await _api.MarkAllNotificationsReadAsync();
        TempData["SuccessMessage"] = "Kõik teated on märgitud loetuks.";
        return RedirectToAction(nameof(Index));
    }
}
