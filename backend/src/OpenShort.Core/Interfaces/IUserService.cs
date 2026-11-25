using Microsoft.AspNetCore.Identity;
using OpenShort.Core.Entities;

namespace OpenShort.Core.Interfaces;

// A user who can sign in, and whether it has the Admin role
public record UserAccount(AppUser User, bool IsAdmin);

public interface IUserService
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync();
    Task<AppUser?> GetByIdAsync(string id);
    Task<(AppUser? User, IEnumerable<IdentityError> Errors)> CreateAdminAsync(string email, string password);
    Task<(bool Success, IEnumerable<IdentityError> Errors)> DeleteAsync(string id);
    Task<(bool Success, IEnumerable<IdentityError> Errors)> DisableTwoFactorAsync(string id);
}
