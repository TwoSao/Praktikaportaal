using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
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

    // ── login ───────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToLocal(returnUrl, User.FindFirstValue(ClaimTypes.Role))
            : View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var result = await _api.LoginAsync(new LoginDto { Email = model.Email, Password = model.Password });
            await SignInAsync(result);
            return RedirectToLocal(model.ReturnUrl, result.User.Role.ToString());
        }
        catch (ApiException ex) when (ex.IsUnauthorized || ex.IsUserError)
        {
            // Same wording for "unknown e-mail" and "wrong password", as the API does.
            ModelState.AddModelError(string.Empty, "Vale e-posti aadress või parool.");
            model.Password = string.Empty;
            return View(model);
        }
    }

    // ── registration ────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult RegisterStudent() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new RegisterStudentViewModel());

    [HttpPost]
    public async Task<IActionResult> RegisterStudent(RegisterStudentViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var result = await _api.RegisterStudentAsync(new RegisterStudentDto
            {
                Email = model.Email, Password = model.Password, FirstName = model.FirstName, LastName = model.LastName,
                SchoolName = model.SchoolName, StudyProgram = model.StudyProgram, Course = model.Course,
                PhoneNumber = Blank(model.PhoneNumber), Location = Blank(model.Location)
            });
            await SignInAsync(result);
            TempData["SuccessMessage"] = $"Tere tulemast, {result.User.FirstName}! Konto on loodud.";
            return RedirectToAction("Index", "Internships");
        }
        catch (ApiException ex) when (ex.IsUserError)
        {
            ModelState.AddApiError(ex);
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult RegisterCompany() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new RegisterCompanyViewModel());

    [HttpPost]
    public async Task<IActionResult> RegisterCompany(RegisterCompanyViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var result = await _api.RegisterCompanyAsync(new RegisterCompanyDto
            {
                Email = model.Email, Password = model.Password, FirstName = model.FirstName, LastName = model.LastName,
                CompanyName = model.CompanyName, RegistrationCode = model.RegistrationCode,
                Description = Blank(model.Description), Website = Blank(model.Website), Address = Blank(model.Address),
                ContactEmail = Blank(model.ContactEmail), ContactPhone = Blank(model.ContactPhone)
            });
            await SignInAsync(result);
            TempData["SuccessMessage"] = $"Tere tulemast, {result.User.FirstName}! Ettevõtte konto on loodud.";
            return RedirectToAction("Index", "Company");
        }
        catch (ApiException ex) when (ex.IsUserError)
        {
            ModelState.AddApiError(ex);
            return View(model);
        }
    }

    // ── logout & access denied ──────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "Oled välja logitud.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores the user's data and the API's JWT in the auth cookie. The cookie expires when the JWT does,
    /// so the site never keeps a token the API would reject.
    /// </summary>
    private async Task SignInAsync(AuthResultDto result)
    {
        var user = result.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(BearerTokenHandler.TokenClaim, result.Token)
        };
        if (user.ProfileId.HasValue)
            claims.Add(new Claim("profile_id", user.ProfileId.Value.ToString()));

        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = JwtExpiry.TryGetExpiry(result.Token) ?? DateTimeOffset.UtcNow.AddHours(24)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            properties);
    }

    /// <summary>Back to where the user came from (local URLs only - no open redirect), otherwise to their own start page.</summary>
    private IActionResult RedirectToLocal(string? returnUrl, string? role)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return role switch
        {
            nameof(UserRole.Company) => RedirectToAction("Index", "Company"),
            nameof(UserRole.Admin) => RedirectToAction("Statistics", "Admin"),
            _ => RedirectToAction("Index", "Internships")
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
