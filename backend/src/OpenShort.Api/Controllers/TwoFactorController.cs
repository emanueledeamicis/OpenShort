using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenShort.Api.Auth;
using OpenShort.Api.Models;
using OpenShort.Core.Entities;

namespace OpenShort.Api.Controllers;

/// <summary>
/// Two-factor authentication of the signed-in user: a time-based code from an authenticator app.
/// An API key is not an account, so these endpoints accept the dashboard token only.
/// </summary>
[ApiController]
[Route("api/security/two-factor")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TwoFactorController : ControllerBase
{
    public const int RecoveryCodeCount = 10;

    private const string AlreadyEnabledMessage = "Two-factor authentication is already enabled.";
    private const string NotEnabledMessage = "Two-factor authentication is not enabled.";
    private const string SetupNotStartedMessage = "Start the two-factor setup before confirming a code.";
    private const string InvalidCodeMessage = "The code is not valid.";
    private const string IncorrectPasswordMessage = "Incorrect password.";
    private const string DisabledMessage = "Two-factor authentication has been disabled.";
    private const string UnexpectedErrorMessage = "An unexpected error occurred while updating two-factor authentication.";

    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<TwoFactorController> _logger;

    public TwoFactorController(UserManager<AppUser> userManager, ILogger<TwoFactorController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<TwoFactorStatusResponse>> GetStatus()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Unauthorized();
        }

        var isEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        return Ok(new TwoFactorStatusResponse
        {
            IsEnabled = isEnabled,
            RecoveryCodesLeft = isEnabled ? await _userManager.CountRecoveryCodesAsync(user) : 0
        });
    }

    /// <summary>
    /// Creates a new secret for the authenticator app. Nothing is protected until a code is confirmed.
    /// The password is asked here, so that a stolen token alone cannot put a second factor on the account
    /// and keep its owner out.
    /// </summary>
    [HttpPost("setup")]
    [EnableRateLimiting(AuthProtection.RateLimitPolicy)]
    public async Task<ActionResult<TwoFactorSetupResponse>> Setup([FromBody] TwoFactorPasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Unauthorized();
        }

        // The secret in use is never replaced: that would lock the user out of their own account
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return Conflict(new { message = AlreadyEnabledMessage });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Two-factor setup was not started for user {UserId}: incorrect password", user.Id);
            return BadRequest(new { message = IncorrectPasswordMessage });
        }

        var resetResult = await _userManager.ResetAuthenticatorKeyAsync(user);
        if (!resetResult.Succeeded)
        {
            return UnexpectedError(user, resetResult.Errors);
        }

        var sharedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        DoNotStoreResponse();
        return Ok(new TwoFactorSetupResponse
        {
            SharedKey = sharedKey!,
            AuthenticatorUri = TwoFactorCodes.CreateAuthenticatorUri(user.UserName!, sharedKey!)
        });
    }

    /// <summary>
    /// Turns two-factor authentication on once the authenticator app proves it has the secret.
    /// The recovery codes are returned here and cannot be read again.
    /// </summary>
    [HttpPost("enable")]
    [EnableRateLimiting(AuthProtection.RateLimitPolicy)]
    public async Task<ActionResult<TwoFactorRecoveryCodesResponse>> Enable([FromBody] EnableTwoFactorRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Unauthorized();
        }

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return Conflict(new { message = AlreadyEnabledMessage });
        }

        if (string.IsNullOrEmpty(await _userManager.GetAuthenticatorKeyAsync(user)))
        {
            return BadRequest(new { message = SetupNotStartedMessage });
        }

        var isCodeValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            TwoFactorCodes.Normalize(request.Code));
        if (!isCodeValid)
        {
            _logger.LogWarning("Two-factor activation failed for user {UserId}: invalid code", user.Id);
            return BadRequest(new { message = InvalidCodeMessage });
        }

        // The codes come first: if turning it on fails they are left unused, while the opposite order
        // could leave the account protected by a second factor and without any recovery code
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        if (recoveryCodes == null)
        {
            return UnexpectedError(user, []);
        }

        var enableResult = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enableResult.Succeeded)
        {
            return UnexpectedError(user, enableResult.Errors);
        }

        _logger.LogInformation("Two-factor authentication enabled for user {UserId}", user.Id);
        DoNotStoreResponse();
        return Ok(new TwoFactorRecoveryCodesResponse { RecoveryCodes = recoveryCodes.ToList() });
    }

    /// <summary>
    /// Replaces the recovery codes: the previous ones stop working.
    /// </summary>
    [HttpPost("recovery-codes")]
    [EnableRateLimiting(AuthProtection.RateLimitPolicy)]
    public async Task<ActionResult<TwoFactorRecoveryCodesResponse>> RegenerateRecoveryCodes([FromBody] TwoFactorPasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return Conflict(new { message = NotEnabledMessage });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Recovery codes were not regenerated for user {UserId}: incorrect password", user.Id);
            return BadRequest(new { message = IncorrectPasswordMessage });
        }

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        if (recoveryCodes == null)
        {
            return UnexpectedError(user, []);
        }

        _logger.LogInformation("Recovery codes regenerated for user {UserId}", user.Id);
        DoNotStoreResponse();
        return Ok(new TwoFactorRecoveryCodesResponse { RecoveryCodes = recoveryCodes.ToList() });
    }

    [HttpPost("disable")]
    [EnableRateLimiting(AuthProtection.RateLimitPolicy)]
    public async Task<ActionResult> Disable([FromBody] TwoFactorPasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return Conflict(new { message = NotEnabledMessage });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Two-factor authentication was not disabled for user {UserId}: incorrect password", user.Id);
            return BadRequest(new { message = IncorrectPasswordMessage });
        }

        var disableResult = await TwoFactorCodes.DisableAsync(_userManager, user);
        if (!disableResult.Succeeded)
        {
            return UnexpectedError(user, disableResult.Errors);
        }

        _logger.LogInformation("Two-factor authentication disabled for user {UserId}", user.Id);
        return Ok(new { message = DisabledMessage });
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrEmpty(userId) ? null : await _userManager.FindByIdAsync(userId);
    }

    // The secret and the recovery codes must not be kept by a browser or by a proxy
    private void DoNotStoreResponse()
    {
        Response.Headers.CacheControl = "no-store";
    }

    private ObjectResult UnexpectedError(AppUser user, IEnumerable<IdentityError> errors)
    {
        _logger.LogError(
            "Two-factor update failed for user {UserId}: {Errors}",
            user.Id,
            string.Join(", ", errors.Select(error => error.Description)));
        return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: UnexpectedErrorMessage);
    }
}
