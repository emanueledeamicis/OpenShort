using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Interfaces;

namespace OpenShort.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SecurityController : ControllerBase
{
    private const string PasswordMismatchErrorCode = "PasswordMismatch";
    private const string PasswordConfirmationMismatchMessage = "New password and confirmation do not match.";
    private const string UserNotFoundMessage = "User not found.";
    private const string IncorrectCurrentPasswordMessage = "Incorrect current password.";
    private const string PasswordChangedSuccessMessage = "Password changed successfully.";
    private const string UnexpectedPasswordChangeErrorMessage = "An unexpected error occurred while changing the password.";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(UserManager<IdentityUser> userManager, IApiKeyService apiKeyService, ILogger<SecurityController> logger)
    {
        _userManager = userManager;
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    // ==================== API KEY ENDPOINTS ====================

    [HttpGet("apikey")]
    public async Task<ActionResult<ApiKeyInfoResponse>> GetApiKeyInfo()
    {
        var apiKey = await _apiKeyService.GetCurrentKeyAsync();
        
        if (apiKey == null)
        {
            return Ok(new ApiKeyInfoResponse
            {
                Exists = false,
                Prefix = null,
                CreatedAt = null
            });
        }

        return Ok(new ApiKeyInfoResponse
        {
            Exists = true,
            Prefix = apiKey.KeyPrefix,
            CreatedAt = apiKey.CreatedAt
        });
    }

    [HttpPost("apikey")]
    public async Task<ActionResult<ApiKeyGeneratedResponse>> GenerateApiKey()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var plainKey = await _apiKeyService.GenerateNewKeyAsync(userId);
        var apiKey = await _apiKeyService.GetCurrentKeyAsync();

        return Ok(new ApiKeyGeneratedResponse
        {
            Key = plainKey,
            Prefix = apiKey!.KeyPrefix,
            CreatedAt = apiKey.CreatedAt
        });
    }

    // ==================== PASSWORD ENDPOINTS ====================

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new { message = PasswordConfirmationMismatchMessage });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = UserNotFoundMessage });
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error =>
                    error.Code == PasswordMismatchErrorCode ? IncorrectCurrentPasswordMessage : error.Description));
                _logger.LogWarning("Password change failed for user {UserId}: {Errors}", userId, errors);
                return BadRequest(new { message = errors });
            }

            _logger.LogInformation("Password changed successfully for user {UserId}", userId);
            return Ok(new { message = PasswordChangedSuccessMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during password change for user {UserId}", userId);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: UnexpectedPasswordChangeErrorMessage);
        }
    }
}

// ==================== DTOs ====================

public class ApiKeyInfoResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("exists")]
    public bool Exists { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("prefix")]
    public string? Prefix { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }
}

public class ApiKeyGeneratedResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
