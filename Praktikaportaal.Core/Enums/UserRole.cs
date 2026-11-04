namespace Praktikaportaal.Core.Enums;

/// <summary>
/// Defines the role of a user in the system.
/// Service accounts are managed separately and are not a user-facing role.
/// </summary>
public enum UserRole
{
    Student = 0,
    Company = 1,
    Admin = 2
}
