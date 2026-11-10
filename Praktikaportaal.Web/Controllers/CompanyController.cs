using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

/// <summary>Everything a logged-in company does: own internships, applicants, status changes, CVs, live updates.</summary>
[Authorize(Roles = nameof(UserRole.Company))]
public class CompanyController : Controller
{
    private const int PageSize = 10;

    private readonly IApiClient _api;

    public CompanyController(IApiClient api)
    {
        _api = api;
    }

    // ── dashboard ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(new CompanyDashboardViewModel { Internships = await _api.GetMyInternshipsAsync() });

    // ── create / edit ───────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create() => View("Form", await NewFormAsync(new InternshipFormViewModel()));

    [HttpPost]
    public async Task<IActionResult> Create(InternshipFormViewModel form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await _api.CreateInternshipAsync(form.ToCreateDto());
                TempData["SuccessMessage"] = "Kuulutus on avaldatud.";
                return RedirectToAction("Details", "Internships", new { id = created.Id });
            }
            catch (ApiException ex) when (ex.IsUserError)
            {
                ModelState.AddApiError(ex);
            }
        }

        return View("Form", await NewFormAsync(form));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var internship = await _api.GetInternshipAsync(id);
        if (internship.CompanyProfileId != User.ProfileId())
            return RedirectToAction("AccessDenied", "Account"); // somebody else's listing

        var skills = await _api.GetSkillsAsync();
        var form = new InternshipFormViewModel
        {
            Id = internship.Id,
            Title = internship.Title,
            Description = internship.Description,
            Field = internship.Field,
            Requirements = internship.Requirements,
            Location = internship.Location,
            StartDate = internship.StartDate,
            EndDate = internship.EndDate,
            ApplicationDeadline = internship.ApplicationDeadline,
            IsRemote = internship.IsRemote,
            // The API returns skill names; the form works with ids.
            SkillIds = skills.Where(s => internship.Skills.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).Select(s => s.Id).ToList()
        };
        return View("Form", await NewFormAsync(form));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, InternshipFormViewModel form)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                await _api.UpdateInternshipAsync(id, form.ToUpdateDto());
                TempData["SuccessMessage"] = "Muudatused on salvestatud.";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ex.IsUserError)
            {
                ModelState.AddApiError(ex);
            }
        }

        return View("Form", await NewFormAsync(form));
    }

    // ── applicants ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Applicants(Guid id, ApplicationStatus? status, int page = 1)
    {
        var internship = await _api.GetInternshipAsync(id);
        if (internship.CompanyProfileId != User.ProfileId())
            return RedirectToAction("AccessDenied", "Account");

        var result = await _api.GetInternshipApplicationsAsync(id, new ApplicationQueryParameters
        {
            Status = status, Page = Math.Max(1, page), PageSize = PageSize
        });
        return View(new ApplicantsViewModel { Internship = internship, Result = result, Status = status });
    }

    [HttpPost]
    public async Task<IActionResult> SetApplicationStatus(Guid id, ApplicationStatus status, Guid internshipId, ApplicationStatus? filter, int page = 1)
    {
        try
        {
            await _api.SetApplicationStatusAsync(id, status);
            TempData["SuccessMessage"] = $"Avalduse staatus: {status.Label()}.";
        }
        catch (ApiException ex) when (ex.IsUserError)
        {
            TempData["ErrorMessage"] = ex.UserMessage(); // e.g. a transition that is not allowed
        }

        return RedirectToAction(nameof(Applicants), new { id = internshipId, status = filter, page });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadCv(Guid id)
    {
        var file = await _api.DownloadApplicantCvAsync(id);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType, file.FileName);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<InternshipFormViewModel> NewFormAsync(InternshipFormViewModel form)
    {
        form.AllSkills = (await _api.GetSkillsAsync()).ToList();
        form.KnownFields = (await _api.GetInternshipFiltersAsync()).Fields.ToList();
        return form;
    }
}
