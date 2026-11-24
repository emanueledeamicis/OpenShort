using Microsoft.AspNetCore.Identity;
using OpenShort.Core.Entities;

namespace OpenShort.Infrastructure.Services;

/// <summary>
/// Turns two-factor authentication off for an account: by its owner, by another administrator,
/// or at startup for the recovery of the default admin.
/// </summary>
public static class TwoFactorAccounts
{
    // Names under which the Identity user store keeps the secret and the recovery codes (AspNetUserTokens)
    private const string StoreLoginProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
    private const string RecoveryCodesTokenName = "RecoveryCodes";

    /// <summary>
    /// Turns two-factor authentication off, then throws away the secret and the recovery codes.
    /// These are separate saves, in this order: if a later one fails, two-factor authentication is off
    /// and what is left is replaced by the next setup.
    /// </summary>
    public static async Task<IdentityResult> DisableAsync(UserManager<AppUser> userManager, AppUser user)
    {
        var disableResult = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disableResult.Succeeded)
        {
            return disableResult;
        }

        var removeKeyResult = await userManager.RemoveAuthenticationTokenAsync(user, StoreLoginProvider, AuthenticatorKeyTokenName);
        if (!removeKeyResult.Succeeded)
        {
            return removeKeyResult;
        }

        return await userManager.RemoveAuthenticationTokenAsync(user, StoreLoginProvider, RecoveryCodesTokenName);
    }
}
