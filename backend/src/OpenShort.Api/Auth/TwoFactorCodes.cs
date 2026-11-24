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

    // Recovery codes of ASP.NET Core Identity: two groups of five characters with a hyphen in between
    private const int RecoveryCodeGroupLength = 5;

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
        return string.Concat((code ?? string.Empty).Where(character => !char.IsWhiteSpace(character) && character != '-'));
    }

    /// <summary>
    /// Tells a code of the authenticator app, six digits, from anything else, which at login is read
    /// as a recovery code.
    /// </summary>
    public static bool IsAuthenticatorCode(string? code)
    {
        var authenticatorCode = Normalize(code);
        return authenticatorCode.Length == CodeDigits && authenticatorCode.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Checks the code of the authenticator app at login. A code is accepted once.
    /// </summary>
    public static async Task<bool> VerifyAuthenticatorCodeForSignInAsync(
        UserManager<AppUser> userManager,
        UsedTwoFactorCodes usedCodes,
        AppUser user,
        string? code)
    {
        var authenticatorCode = Normalize(code);
        return await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, authenticatorCode)
            && usedCodes.TryUse(user.Id, authenticatorCode);
    }

    /// <summary>
    /// Checks a recovery code at login and uses it up. The code is accepted however it was typed:
    /// in lower case, without the hyphen, with spaces.
    /// </summary>
    public static async Task<bool> RedeemRecoveryCodeForSignInAsync(UserManager<AppUser> userManager, AppUser user, string? code)
    {
        var recoveryCode = Normalize(code).ToUpperInvariant();
        if (recoveryCode.Length == 0)
        {
            return false;
        }

        // The stored codes are in upper case, with the hyphen
        if (recoveryCode.Length == RecoveryCodeGroupLength * 2)
        {
            recoveryCode = $"{recoveryCode[..RecoveryCodeGroupLength]}-{recoveryCode[RecoveryCodeGroupLength..]}";
        }

        return (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode)).Succeeded;
    }
}
