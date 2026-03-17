using OpenShort.Core.Entities;

namespace OpenShort.Core.Interfaces;

public interface ITokenService
{
    Task<string> CreateTokenAsync(AppUser user, IList<string> roles);
}
