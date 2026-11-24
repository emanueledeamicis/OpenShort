using Microsoft.AspNetCore.Identity;
using OpenShort.Core.Entities;

namespace OpenShort.Api.Auth;

/// <summary>
/// Helpers for the time-based codes (TOTP) of an authenticator app.
/// The codes themselves are generated and verified by ASP.NET Core Identity.
/// </summary>
public static class TwoFactorCodes
{
    public const string Issuer = "OpenShort";

    private const int CodeDigits = 6;

    // Names under which the Identity user store keeps the secret and the recovery codes (AspNetUserTokens)
    private const string StoreLoginProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
    private const string RecoveryCodesTokenName = "RecoveryCodes";

    /// <summary>
    /// Registers the Identity provider that verifies the codes, for a UserManager built from these options
    /// alone. In the application AddApiEndpoints registers the same provider as a service, which replaces
    /// the instance given here.
    /// </summary>
    public static void ConfigureIdentity(IdentityOptions options)
    {
        options.Tokens.ProviderMap[TokenOptions.DefaultAuthenticatorProvider] =
            new TokenProviderDescriptor(typeof(AuthenticatorTokenProvider<AppUser>))
            {
                ProviderInstance = new AuthenticatorTokenProvider<AppUser>()
            };
    }

    /// <summary>
    /// Builds the otpauth URI that authenticator apps read from a QR code.
    /// </summary>
    public static string CreateAuthenticatorUri(string userName, string sharedKey)
    {
        var issuer = Uri.EscapeDataString(Issuer);
        return $"otpauth://totp/{issuer}:{Uri.EscapeDataString(userName)}?secret={sharedKey}&issuer={issuer}&digits={CodeDigits}";
    }

    /// <summary>
    /// Authenticator apps show the code in groups: spaces and hyphens are not part of it.
    /// </summary>
    public static string Normalize(string? code)
    {
        return (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
    }

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
