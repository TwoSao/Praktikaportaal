using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

[Authorize(Roles = "Company")]
public class CompanyController : Controller
{
    private readonly IApiClient _api;

    public CompanyController(IApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var internships = await _api.GetMyInternshipsAsync();
        return View(internships);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateInternshipViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateInternshipViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (success, error, created) = await _api.CreateInternshipAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Kuulutuse loomine ebaõnnestus.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Praktikakuulutus on edukalt avaldatud!";
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Applicants(Guid id)
    {
        var internship = await _api.GetInternshipByIdAsync(id);
        if (internship == null)
            return NotFound();

        var applicants = await _api.GetInternshipApplicationsAsync(id);

        ViewBag.Internship = internship;
        return View(applicants);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateApplicantStatus(Guid applicationId, Guid internshipId, string status)
    {
        await _api.UpdateApplicationStatusAsync(applicationId, status);
        TempData["SuccessMessage"] = "Kandidaadi staatus on edukalt uuendatud!";
        return RedirectToAction("Applicants", new { id = internshipId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseInternship(Guid id)
    {
        await _api.UpdateInternshipStatusAsync(id, "Closed");
        TempData["SuccessMessage"] = "Praktikakuulutus on suletud.";
        return RedirectToAction("Dashboard");
    }
}

