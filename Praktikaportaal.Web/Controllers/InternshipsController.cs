using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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

    // ── applying (students) ─────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Student))]
    public async Task<IActionResult> Apply(Guid id)
    {
        var model = await BuildApplyModelAsync(id, coverLetter: null);
        return model.Internship is { } i && IsOpen(i) ? View(model) : NotOpen(id);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Student))]
    public async Task<IActionResult> Apply(Guid id, ApplyViewModel form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _api.ApplyAsync(id, new CreateApplicationDto { CoverLetter = string.IsNullOrWhiteSpace(form.CoverLetter) ? null : form.CoverLetter.Trim() });
                TempData["SuccessMessage"] = "Avaldus on esitatud! Staatust näed lehel \"Minu avaldused\".";
                return RedirectToAction("Applications", "Student");
            }
            catch (ApiException ex) when (ex.IsUserError)
            {
                // 409: already applied; 400: deadline passed / not accepting applications
                ModelState.AddApiError(ex);
            }
        }

        return View(await BuildApplyModelAsync(id, form.CoverLetter));
    }

    private async Task<ApplyViewModel> BuildApplyModelAsync(Guid id, string? coverLetter) => new()
    {
        InternshipId = id,
        CoverLetter = coverLetter,
        Internship = await _api.GetInternshipAsync(id),
        Cv = await _api.GetMyCvAsync()
    };

    private static bool IsOpen(InternshipDto i) =>
        i.Status == InternshipStatus.Published && (!i.ApplicationDeadline.HasValue || i.ApplicationDeadline.Value >= DateTime.UtcNow);

    private IActionResult NotOpen(Guid id)
    {
        TempData["ErrorMessage"] = "See praktikakoht ei võta avaldusi vastu (tähtaeg möödas või kuulutus suletud).";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── manage (owning company) / moderate (admin) ──────────────────────────

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Company) + "," + nameof(UserRole.Admin))]
    public async Task<IActionResult> SetStatus(Guid id, InternshipStatus status, string? returnTo = null)
    {
        try
        {
            await _api.SetInternshipStatusAsync(id, status); // the API checks ownership / admin
            TempData["SuccessMessage"] = $"Kuulutuse staatus: {status.Label()}.";
        }
        catch (ApiException ex) when (ex.IsUserError)
        {
            TempData["ErrorMessage"] = ex.UserMessage();
        }

        return BackTo(returnTo, id);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Company) + "," + nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete(Guid id, string? returnTo = null)
    {
        await _api.DeleteInternshipAsync(id);
        TempData["SuccessMessage"] = "Kuulutus on kustutatud.";
        return User.IsInRole(nameof(UserRole.Company)) ? RedirectToAction("Index", "Company") : RedirectToAction(nameof(Index));
    }

    private IActionResult BackTo(string? returnTo, Guid id) => returnTo switch
    {
        "details" => RedirectToAction(nameof(Details), new { id }),
        _ when User.IsInRole(nameof(UserRole.Company)) => RedirectToAction("Index", "Company"),
        _ => RedirectToAction(nameof(Details), new { id })
    };
}
