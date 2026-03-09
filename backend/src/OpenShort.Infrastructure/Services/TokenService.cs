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
    private readonly IJwtKeyProvider _jwtKeyProvider;

    public TokenService(IConfiguration config, IJwtKeyProvider jwtKeyProvider)
    {
        _config = config;
        _jwtKeyProvider = jwtKeyProvider;
    }

    public async Task<string> CreateTokenAsync(IdentityUser user, IList<string> roles)
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

        // Get dynamic or auto-generated key
        var secretKey = await _jwtKeyProvider.GetOrGenerateKeyAsync();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        // Credentials
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

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
