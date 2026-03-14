using System.ComponentModel.DataAnnotations;

namespace OpenShort.Api.Models;

public class LoginRequest
{
    [Required]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
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
