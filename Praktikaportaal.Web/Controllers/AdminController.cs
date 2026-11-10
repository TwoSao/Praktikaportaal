using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminController : Controller
{
    private readonly IApiClient _api;

    public AdminController(IApiClient api)
    {
        _api = api;
    }

    /// <summary>Application statistics: totals, by status, top internships, by field and over time.</summary>
    [HttpGet]
    public async Task<IActionResult> Statistics([FromQuery] StatisticsQueryParameters query)
    {
        // Weeks suit a few weeks of data better than the API's default (months).
        if (!Request.Query.ContainsKey("period"))
            query.Period = StatisticsPeriod.Week;
        query.Top = Math.Clamp(query.Top, 1, 50);

        if (query.From.HasValue && query.To.HasValue && query.To < query.From)
        {
            ModelState.AddModelError(string.Empty, "Lõppkuupäev ei tohi olla enne alguskuupäeva.");
            query.From = query.To = null; // show the unfiltered numbers instead of an empty page
        }

        try
        {
            return View(new StatisticsViewModel { Stats = await _api.GetStatisticsAsync(query), Query = query });
        }
        catch (ApiException ex) when (ex.IsUserError)
        {
            // e.g. a range that is too long for daily buckets
            ModelState.AddModelError(string.Empty, ex.UserMessage());
            query.From = query.To = null;
            query.Period = StatisticsPeriod.Month;
            return View(new StatisticsViewModel { Stats = await _api.GetStatisticsAsync(query), Query = query });
        }
    }
}
