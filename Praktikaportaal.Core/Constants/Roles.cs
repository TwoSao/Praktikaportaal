using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Constants;

/// <summary>
/// Role names used in JWT role claims and [Authorize(Roles = ...)] attributes.
/// Values match <see cref="UserRole"/> names.
/// </summary>
public static class Roles
{
    public const string Student = nameof(UserRole.Student);
    public const string Company = nameof(UserRole.Company);
    public const string Admin = nameof(UserRole.Admin);
    public const string Service = nameof(UserRole.Service);

    /// <summary>Company that owns the resource, or an Admin moderating it.</summary>
    public const string CompanyOrAdmin = Company + "," + Admin;

    /// <summary>Back-office access: Admin or the Worker service account.</summary>
    public const string ServiceOrAdmin = Service + "," + Admin;
}
