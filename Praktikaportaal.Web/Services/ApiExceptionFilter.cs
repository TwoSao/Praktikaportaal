using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Praktikaportaal.Web.Services;

/// <summary>
/// Turns API errors that a controller did not handle into a sensible page:
/// 401 → log in again, 403 → access denied, 404 → "not found", API down → friendly message.
/// Expected business errors (400/409 on a form) are handled by the controller next to the form.
/// </summary>
public class ApiExceptionFilter : IAsyncExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly IModelMetadataProvider _metadata;
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(
        ITempDataDictionaryFactory tempDataFactory,
        IModelMetadataProvider metadata,
        ILogger<ApiExceptionFilter> logger)
    {
        _tempDataFactory = tempDataFactory;
        _metadata = metadata;
        _logger = logger;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ApiException { IsUnauthorized: true }:
                // The JWT in the cookie expired or was rejected: drop the cookie and ask for a new login.
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                _tempDataFactory.GetTempData(context.HttpContext)["ErrorMessage"] = "Sessioon aegus. Palun logi uuesti sisse.";
                context.Result = new RedirectToActionResult("Login", "Account",
                    new { returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString });
                break;

            case ApiException { IsForbidden: true }:
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                break;

            case ApiException { IsNotFound: true }:
                context.Result = View(context, "NotFound", StatusCodes.Status404NotFound, null);
                break;

            case ApiException ex:
                _logger.LogWarning(ex, "Unhandled API error {Status} on {Path}", (int)ex.StatusCode, context.HttpContext.Request.Path);
                context.Result = View(context, "ApiError", (int)ex.StatusCode, ex.Detail ?? ex.Title);
                break;

            case ApiUnavailableException ex:
                _logger.LogError(ex, "The API is not reachable");
                context.Result = View(context, "ApiError", StatusCodes.Status503ServiceUnavailable, ex.Message);
                break;

            default:
                return; // not ours: the normal error page handles it
        }

        context.ExceptionHandled = true;
    }

    private ViewResult View(ExceptionContext context, string viewName, int statusCode, string? message) => new()
    {
        ViewName = viewName,
        StatusCode = statusCode,
        ViewData = new ViewDataDictionary(_metadata, context.ModelState) { Model = message }
    };
}
