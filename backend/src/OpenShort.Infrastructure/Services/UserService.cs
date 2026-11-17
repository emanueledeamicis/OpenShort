using Microsoft.AspNetCore.Identity;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;

namespace OpenShort.Infrastructure.Services;

public class UserService : IUserService
{
    private const string UserNotFoundErrorCode = "NotFound";
    private const string UserNotFoundErrorDescription = "User not found.";
    private const string LastAdminDeletionForbiddenErrorCode = "LastAdminDeletionForbidden";
    private const string LastAdminDeletionForbiddenErrorDescription = "You cannot delete the last admin user who can sign in.";

    private readonly UserManager<AppUser> _userManager;

    public UserService(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IEnumerable<AppUser>> GetAllAdminsAsync()
    {
        return await _userManager.GetUsersInRoleAsync(DatabaseInitializer.AdminRoleName);
    }

    public async Task<AppUser?> GetByIdAsync(string id)
    {
        return await _userManager.FindByIdAsync(id);
    }

    public async Task<(AppUser? User, IEnumerable<IdentityError> Errors)> CreateAdminAsync(string email, string password)
    {
        var user = new AppUser { UserName = email, Email = email, CreatedAt = DateTime.UtcNow };
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return (null, result.Errors);
        }

        var roleAssignmentResult = await _userManager.AddToRoleAsync(user, DatabaseInitializer.AdminRoleName);
        if (roleAssignmentResult.Succeeded)
        {
            return (user, Enumerable.Empty<IdentityError>());
        }

        await _userManager.DeleteAsync(user);
        return (null, roleAssignmentResult.Errors);
    }

    public async Task<(bool Success, IEnumerable<IdentityError> Errors)> DeleteAdminAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return (false, new []
            {
                new IdentityError
                {
                    Code = UserNotFoundErrorCode,
                    Description = UserNotFoundErrorDescription
                }
            });
        }

        if (!await _userManager.IsInRoleAsync(user, DatabaseInitializer.AdminRoleName))
        {
            return (false, new []
            {
                new IdentityError
                {
                    Code = UserNotFoundErrorCode,
                    Description = UserNotFoundErrorDescription
                }
            });
        }

        // An administrator without a password cannot sign in, so it does not count as a remaining one
        var remainingAdmins = await AdminAccounts.GetAbleToSignInAsync(_userManager);
        if (remainingAdmins.All(admin => admin.Id == user.Id))
        {
            return (false, new []
            {
                new IdentityError
                {
                    Code = LastAdminDeletionForbiddenErrorCode,
                    Description = LastAdminDeletionForbiddenErrorDescription
                }
            });
        }

        var result = await _userManager.DeleteAsync(user);
        
        return (result.Succeeded, result.Errors);
    }
}
