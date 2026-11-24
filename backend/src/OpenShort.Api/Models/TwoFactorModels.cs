using System.ComponentModel.DataAnnotations;

namespace OpenShort.Api.Models;

public class TwoFactorStatusResponse
{
    public bool IsEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
}

public class TwoFactorSetupResponse
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
}

public class TwoFactorRecoveryCodesResponse
{
    public IReadOnlyList<string> RecoveryCodes { get; set; } = [];
}

public class EnableTwoFactorRequest
{
    public const int MaxCodeLength = 16;

    [Required]
    [StringLength(MaxCodeLength)]
    public string Code { get; set; } = string.Empty;
}

public class TwoFactorPasswordRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;
}
