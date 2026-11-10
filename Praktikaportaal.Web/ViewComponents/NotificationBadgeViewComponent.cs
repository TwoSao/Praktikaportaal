using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.ViewComponents;

/// <summary>Navbar link "Teated" with the number of unread notifications.</summary>
public class NotificationBadgeViewComponent : ViewComponent
{
    private readonly IApiClient _api;
    private readonly ILogger<NotificationBadgeViewComponent> _logger;

    public NotificationBadgeViewComponent(IApiClient api, ILogger<NotificationBadgeViewComponent> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        int unread = 0;
        try
        {
            // pageSize 1: only the unread counter is needed
            unread = (await _api.GetNotificationsAsync(new NotificationQueryParameters { PageSize = 1 })).UnreadCount;
        }
        catch (Exception ex) when (ex is ApiException or ApiUnavailableException)
        {
            // A broken badge must never break the page around it.
            _logger.LogDebug(ex, "Could not load the unread notification count");
        }

        return View(unread);
    }
}
