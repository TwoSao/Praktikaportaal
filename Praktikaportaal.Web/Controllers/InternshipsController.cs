using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

public class InternshipsController : Controller
{
    private readonly IApiClient _api;

    public InternshipsController(IApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool? isRemote)
    {
        ViewBag.Search = search;
        ViewBag.IsRemote = isRemote;

        var list = await _api.GetPublishedInternshipsAsync(search, isRemote);
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var item = await _api.GetInternshipByIdAsync(id);
        if (item == null)
            return NotFound();

        return View(item);
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Apply(Guid id)
    {
        var item = await _api.GetInternshipByIdAsync(id);
        if (item == null)
            return NotFound();

        var model = new ApplyViewModel
        {
            InternshipId = item.Id,
            InternshipTitle = item.Title,
            CompanyName = item.CompanyName
        };

        return View(model);
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (success, error) = await _api.ApplyAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Kandideerimine ebaõnnestus.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Sinu kandideerimisavaldus on edukalt esitatud!";
        return RedirectToAction("Dashboard", "Student");
    }
}

