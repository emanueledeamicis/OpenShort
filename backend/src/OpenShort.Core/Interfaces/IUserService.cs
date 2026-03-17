using Microsoft.AspNetCore.Identity;
using OpenShort.Core.Entities;

namespace OpenShort.Core.Interfaces;

public interface IUserService
{
    Task<IEnumerable<AppUser>> GetAllAdminsAsync();
    Task<AppUser?> GetByIdAsync(string id);
    Task<(AppUser? User, IEnumerable<IdentityError> Errors)> CreateAdminAsync(string email, string password);
    Task<(bool Success, IEnumerable<IdentityError> Errors)> DeleteAdminAsync(string id);
}
