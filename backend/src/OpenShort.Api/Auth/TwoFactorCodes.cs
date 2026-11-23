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
    /// Registers the Identity provider that verifies the codes. The instance is given here,
    /// so every UserManager configured with these options has it without a service registration.
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
    /// Turns two-factor authentication off and throws away the secret and the recovery codes,
    /// so that turning it on again always starts from a new secret.
    /// </summary>
    public static async Task<IdentityResult> DisableAsync(UserManager<AppUser> userManager, AppUser user)
    {
        var disableResult = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disableResult.Succeeded)
        {
            return disableResult;
        }

        var removeCodesResult = await userManager.RemoveAuthenticationTokenAsync(user, StoreLoginProvider, RecoveryCodesTokenName);
        if (!removeCodesResult.Succeeded)
        {
            return removeCodesResult;
        }

        return await userManager.RemoveAuthenticationTokenAsync(user, StoreLoginProvider, AuthenticatorKeyTokenName);
    }
}
