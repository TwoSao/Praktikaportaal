using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

/// <summary>
/// The authenticated caller as seen by the services layer (built from JWT claims in the API).
/// ProfileId is the StudentProfile or CompanyProfile id, null for Admin/Service.
/// </summary>
public record CurrentUser(Guid UserId, UserRole Role, Guid? ProfileId);
