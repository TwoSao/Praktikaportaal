using Praktikaportaal.Core.Entities;

namespace Praktikaportaal.Core.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user, Guid? profileId);
}

