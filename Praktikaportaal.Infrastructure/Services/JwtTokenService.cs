using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user, Guid? profileId)
    {
        string secretKey = _configuration["Jwt:Secret"]
            ?? "Praktikaportaal_Super_Secret_Key_For_Development_Environment_2026_AtLeast32Bytes!";
        string issuer = _configuration["Jwt:Issuer"] ?? "Praktikaportaal";
        string audience = _configuration["Jwt:Audience"] ?? "PraktikaportaalApp";
        int expiryHours = int.TryParse(_configuration["Jwt:ExpiresInHours"], out int h) ? h : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        if (profileId.HasValue)
        {
            claims.Add(new Claim("profile_id", profileId.Value.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(expiryHours),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
