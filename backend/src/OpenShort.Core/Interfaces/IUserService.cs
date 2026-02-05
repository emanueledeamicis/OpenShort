using Microsoft.AspNetCore.Identity;

namespace OpenShort.Core.Interfaces;

public interface IUserService
{
    Task<IEnumerable<IdentityUser>> GetAllAsync();
    Task<IdentityUser?> GetByIdAsync(string id);
    Task<(IdentityUser? User, IEnumerable<IdentityError> Errors)> CreateAsync(string email, string password);
    Task<(bool Success, IEnumerable<IdentityError> Errors)> DeleteAsync(string id);
}
