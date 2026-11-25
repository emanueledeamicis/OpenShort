using System.ComponentModel.DataAnnotations;

namespace OpenShort.Api.Models;

public class LoginRequest
{
    public const int MaxCodeLength = 32;

    [Required]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Code from the authenticator app, or a recovery code. Only for accounts with two-factor authentication.
    /// </summary>
    [StringLength(MaxCodeLength)]
    public string? Code { get; set; }
}

/// <summary>
/// Answer to a login with the right password that still needs a valid second factor.
/// </summary>
public class TwoFactorRequiredResponse
{
    public string Message { get; set; } = string.Empty;
    public bool RequiresTwoFactor { get; set; } = true;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string UserName { get; set; } = string.Empty;
}

public class InitialSetupStatusResponse
{
    public bool IsSetupRequired { get; set; }
    public string UserName { get; set; } = string.Empty;
}

public class SetupAdminRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
}
