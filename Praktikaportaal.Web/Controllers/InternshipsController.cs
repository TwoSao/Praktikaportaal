using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

/// <summary>Public internship catalogue: search, filter, sort, paginate, details.</summary>
public class InternshipsController : Controller
{
    private const int PageSize = 9;

    private readonly IApiClient _api;

    public InternshipsController(IApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] InternshipQueryParameters query)
    {
        if (!Request.Query.ContainsKey("pageSize"))
            query.PageSize = PageSize;

        // A page number or size that the API would reject (e.g. ?page=0) is brought into range instead of showing an error.
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, PagingParameters.MaxPageSize);

        var result = await _api.SearchInternshipsAsync(query);

        // Asked for a page behind the last one (e.g. after filtering): show the last page.
        if (result.Items.Count == 0 && result.TotalCount > 0 && query.Page > result.TotalPages)
        {
            query.Page = result.TotalPages;
            result = await _api.SearchInternshipsAsync(query);
        }

        return View(new InternshipListViewModel
        {
            Result = result,
            Query = query,
            Filters = await _api.GetInternshipFiltersAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var internship = await _api.GetInternshipAsync(id);

        bool isAdmin = User.IsInRole(nameof(UserRole.Admin));
        bool isOwner = User.IsInRole(nameof(UserRole.Company)) && User.ProfileId() == internship.CompanyProfileId;

        ApplicationDto? existing = null;
        if (User.IsInRole(nameof(UserRole.Student)))
        {
            // Show "you have applied" instead of a second Apply button.
            var mine = await _api.GetMyApplicationsAsync(new ApplicationQueryParameters { PageSize = PagingParameters.MaxPageSize });
            existing = mine.Items.FirstOrDefault(a => a.InternshipId == id && a.Status != ApplicationStatus.Withdrawn);
        }

        return View(new InternshipDetailsViewModel
        {
            Internship = internship,
            ExistingApplication = existing,
            CanManage = isOwner || isAdmin,
            IsAdmin = isAdmin
        });
    }
}
