using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

public class HomeController : Controller
{
    private readonly IApiClient _api;

    public HomeController(IApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index()
    {
        // The start page must still open when the API is down, just without the internship list.
        try
        {
            var latest = await _api.SearchInternshipsAsync(new InternshipQueryParameters { PageSize = 6 });
            var filters = await _api.GetInternshipFiltersAsync();
            return View(new HomeViewModel { Latest = latest.Items, TotalInternships = latest.TotalCount, Filters = filters });
        }
        catch (ApiUnavailableException)
        {
            return View(new HomeViewModel { ApiAvailable = false });
        }
    }

    /// <summary>Friendly page for status codes without a body (404 for an unknown URL, etc.).</summary>
    [Route("Home/Status/{id:int}")]
    public IActionResult Status(int id)
    {
        Response.StatusCode = id;
        return id == StatusCodes.Status404NotFound
            ? View("NotFound")
            : View("ApiError", model: id == StatusCodes.Status403Forbidden ? "Sul ei ole sellele lehele ligipääsu." : "Midagi läks valesti.");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
