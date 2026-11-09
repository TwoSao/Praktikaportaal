namespace Praktikaportaal.Core.Enums;

/// <summary>
/// Defines the role of a user in the system.
/// Service is a non-interactive account used by the Worker Service to call the API.
/// </summary>
public enum UserRole
{
    Student = 0,
    Company = 1,
    Admin = 2,
    Service = 3
}
