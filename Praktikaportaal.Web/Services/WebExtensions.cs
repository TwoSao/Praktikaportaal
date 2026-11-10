using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Praktikaportaal.Core.DTOs;

namespace Praktikaportaal.Web.Services;

public static class WebExtensions
{
    /// <summary>The StudentProfile / CompanyProfile id stored in the login cookie.</summary>
    public static Guid? ProfileId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("profile_id"), out var id) ? id : null;

    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// Shows an API error next to the form: validation errors on their fields (the API names the DTO property, which
    /// view models mirror), everything else as a general message.
    /// </summary>
    public static void AddApiError(this ModelStateDictionary modelState, ApiException ex)
    {
        foreach (var (field, messages) in ex.Errors)
        {
            // "Password" or "$.skills[0].skillId" -> use the last simple segment as the model key
            string key = field.Contains('.') ? field[(field.LastIndexOf('.') + 1)..] : field;
            foreach (string message in messages)
                modelState.AddModelError(modelState.ContainsKey(key) ? key : string.Empty, ApiMessages.Translate(message));
        }

        if (ex.Errors.Count == 0)
            modelState.AddModelError(string.Empty, ex.UserMessage());
    }

    /// <summary>Builds the route values of a page link: the current query string with another <c>page</c>.</summary>
    public static Dictionary<string, string?> WithPage(this IQueryCollection query, int page)
    {
        var values = query
            .Where(q => !string.Equals(q.Key, "page", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(q => q.Key, q => (string?)q.Value.ToString());
        values["page"] = page.ToString();
        return values;
    }

    public static string DisplayName(this ClaimsPrincipal user) => user.Identity?.Name ?? "";
}
