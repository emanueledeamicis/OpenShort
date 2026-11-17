using Microsoft.AspNetCore.Identity;
using OpenShort.Core.Entities;

namespace OpenShort.Infrastructure.Services;

/// <summary>
/// Tells which administrators can actually sign in. An administrator without a password cannot,
/// so it must not count when deciding whether the instance still has someone able to manage it.
/// </summary>
public static class AdminAccounts
{
    public static async Task<IReadOnlyList<AppUser>> GetAbleToSignInAsync(UserManager<AppUser> userManager)
    {
        var administrators = await userManager.GetUsersInRoleAsync(DatabaseInitializer.AdminRoleName);
        return administrators.Where(administrator => !string.IsNullOrEmpty(administrator.PasswordHash)).ToList();
    }

    public static async Task<bool> AnyAbleToSignInAsync(UserManager<AppUser> userManager)
    {
        return (await GetAbleToSignInAsync(userManager)).Count > 0;
    }
}
