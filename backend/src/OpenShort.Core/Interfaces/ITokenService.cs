using Microsoft.AspNetCore.Identity;

namespace OpenShort.Core.Interfaces;

public interface ITokenService
{
    string CreateToken(IdentityUser user, IList<string> roles);
}
