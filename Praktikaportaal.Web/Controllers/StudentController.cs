using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly IApiClient _api;

    public StudentController(IApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var applications = await _api.GetMyApplicationsAsync();
        return View(applications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        bool success = await _api.WithdrawApplicationAsync(id);
        if (success)
        {
            TempData["SuccessMessage"] = "Kandideerimisavaldus on tagasi võetud.";
        }
        else
        {
            TempData["ErrorMessage"] = "Avalduse tagasivõtmine ebaõnnestus.";
        }

        return RedirectToAction("Dashboard");
    }
}

