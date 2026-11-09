using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Web.Models;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.Controllers;

public class AccountController : Controller
{
    private readonly IApiClient _api;

    public AccountController(IApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (success, token, error, user) = await _api.LoginAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Sisselogimine ebaõnnestus.");
            return View(model);
        }

        await SignInUserAsync(token!, user);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        string role = user.GetProperty("role").GetString();
        return role switch
        {
            "Company" => RedirectToAction("Dashboard", "Company"),
            "Student" => RedirectToAction("Dashboard", "Student"),
            _ => RedirectToAction("Index", "Internships")
        };
    }

    [HttpGet]
    public IActionResult RegisterStudent()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new RegisterStudentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterStudent(RegisterStudentViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (success, token, error, user) = await _api.RegisterStudentAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Registreerimine ebaõnnestus.");
            return View(model);
        }

        await SignInUserAsync(token!, user);
        return RedirectToAction("Dashboard", "Student");
    }

    [HttpGet]
    public IActionResult RegisterCompany()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new RegisterCompanyViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterCompany(RegisterCompanyViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (success, token, error, user) = await _api.RegisterCompanyAsync(model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Registreerimine ebaõnnestus.");
            return View(model);
        }

        await SignInUserAsync(token!, user);
        return RedirectToAction("Dashboard", "Company");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SignInUserAsync(string token, dynamic user)
    {
        string id = user.GetProperty("id").GetString()!;
        string email = user.GetProperty("email").GetString()!;
        string firstName = user.GetProperty("firstName").GetString()!;
        string lastName = user.GetProperty("lastName").GetString()!;
        string role = user.GetProperty("role").GetString()!;
        string? profileId = user.TryGetProperty("profileId", out System.Text.Json.JsonElement p) && p.ValueKind != System.Text.Json.JsonValueKind.Null ? p.GetString() : null;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id),
            new(ClaimTypes.Name, $"{firstName} {lastName}"),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("jwt_token", token)
        };

        if (!string.IsNullOrEmpty(profileId))
        {
            claims.Add(new Claim("profile_id", profileId));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
            });
    }
}

