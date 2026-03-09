using Microsoft.AspNetCore.Identity;

namespace OpenShort.Core.Interfaces;

public interface ITokenService
{
    Task<string> CreateTokenAsync(IdentityUser user, IList<string> roles);
}
