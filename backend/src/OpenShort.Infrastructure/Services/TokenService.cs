using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OpenShort.Core.Interfaces;

namespace OpenShort.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly SymmetricSecurityKey _key;

    public TokenService(IConfiguration config)
    {
        _config = config;
        var tokenKey = _config["Jwt:Key"] ?? "SuperSecretKeyForDevelopmentOnly_ChangeInProduction_AtLeast32CharsLong";
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey));
    }

    public string CreateToken(IdentityUser user, IList<string> roles)
    {
        // Claims
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id), // Standard Subject claim
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique Token ID
            new Claim(ClaimTypes.NameIdentifier, user.Id), // For ASP.NET Identity compatibility
            new Claim(ClaimTypes.Name, user.UserName ?? "")
        };

        // Add Roles
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Credentials
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256Signature);

        // Descriptor
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7), // Token valid for 7 days
            SigningCredentials = creds,
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"]
        };

        // Handler
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
