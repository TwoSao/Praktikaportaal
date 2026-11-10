using System.Collections;
using System.Globalization;
using System.Reflection;

namespace Praktikaportaal.Web.Services;

/// <summary>Turns a query object (e.g. <c>InternshipQueryParameters</c>) into <c>?a=1&amp;b=x</c>, skipping null/empty values.</summary>
public static class QueryStringBuilder
{
    public static string Build(object? query)
    {
        if (query == null)
            return string.Empty;

        var parts = new List<string>();
        foreach (var property in query.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            string? value = Format(property.GetValue(query));
            if (!string.IsNullOrEmpty(value))
                parts.Add($"{Uri.EscapeDataString(CamelCase(property.Name))}={Uri.EscapeDataString(value)}");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        bool b => b ? "true" : "false",
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable => null, // lists are not part of any query used here
        _ => value.ToString()
    };

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
