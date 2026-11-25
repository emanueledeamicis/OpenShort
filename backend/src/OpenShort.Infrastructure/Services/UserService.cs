using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;

namespace OpenShort.Infrastructure.Services;

public class UserService : IUserService
{
    private const string UserNotFoundErrorCode = "NotFound";
    private const string UserNotFoundErrorDescription = "User not found.";
    private const string LastAdminDeletionForbiddenErrorCode = "LastAdminDeletionForbidden";
    private const string LastAdminDeletionForbiddenErrorDescription = "You cannot delete the last admin user who can sign in.";
    public const string TwoFactorNotEnabledErrorCode = "TwoFactorNotEnabled";
    private const string TwoFactorNotEnabledErrorDescription = "Two-factor authentication is not enabled for this user.";

    private readonly UserManager<AppUser> _userManager;

    public UserService(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    // Every user, not the administrators only: a user without the role can sign in as well.
    // In no particular order: the Users page sorts them
    public async Task<IReadOnlyList<UserAccount>> GetAllAsync()
    {
        var administrators = await _userManager.GetUsersInRoleAsync(DatabaseInitializer.AdminRoleName);
        var administratorIds = administrators.Select(administrator => administrator.Id).ToHashSet();
        var users = await _userManager.Users.ToListAsync();

        return users.Select(user => new UserAccount(user, administratorIds.Contains(user.Id))).ToList();
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

    public async Task<(bool Success, IEnumerable<IdentityError> Errors)> DeleteAsync(string id)
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

        // An administrator without a password cannot sign in, so it does not count as a remaining one.
        // A user without the role never counts: deleting it leaves the administrators as they are
        var remainingAdmins = await AdminAccounts.GetAbleToSignInAsync(_userManager);
        if (await _userManager.IsInRoleAsync(user, DatabaseInitializer.AdminRoleName)
            && remainingAdmins.All(admin => admin.Id == user.Id))
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

    public async Task<(bool Success, IEnumerable<IdentityError> Errors)> DisableTwoFactorAsync(string id)
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

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return (false, new []
            {
                new IdentityError
                {
                    Code = TwoFactorNotEnabledErrorCode,
                    Description = TwoFactorNotEnabledErrorDescription
                }
            });
        }

        // Off with errors means that the secret or the recovery codes were not removed afterwards
        var result = await TwoFactorAccounts.DisableAsync(_userManager, user);

        return (result.IsDisabled, result.Errors);
    }
}
