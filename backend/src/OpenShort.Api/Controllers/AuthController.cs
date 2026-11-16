using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using OpenShort.Api.Auth;
using OpenShort.Api.Models;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private const string InvalidCredentialsMessage = "Invalid username/email or password";
    private const string AccountLockedMessage = "Too many failed login attempts. Please try again later.";
    private const string PasswordConfirmationMismatchMessage = "Password and confirmation do not match.";
    private const string InitialAdminSetupCompletedMessage = "Initial admin setup has already been completed.";
    private const string AdminUserUnavailableMessage = "Admin user is not available.";
    private const string AdminPasswordAlreadyConfiguredMessage = "Admin password has already been configured.";
    private const string InitialAdminSetupDescription = "Indicates whether the initial admin password setup flow is still required.";
    private const string UnexpectedLoginErrorMessage = "An unexpected error occurred during login.";
    private const string UnexpectedAdminSetupErrorMessage = "An unexpected error occurred during admin setup.";
    private const string UnexpectedRegistrationErrorMessage = "An unexpected error occurred during registration.";

    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ISettingService _settingService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        ISettingService settingService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _settingService = settingService;
        _logger = logger;
    }

    [HttpGet("setup-status")]
    public async Task<ActionResult<InitialSetupStatusResponse>> GetSetupStatus()
    {
        var isSetupRequired = await _settingService.GetSettingAsync(DatabaseInitializer.InitialAdminSetupRequiredKey, true);

        return Ok(new InitialSetupStatusResponse
        {
            IsSetupRequired = isSetupRequired,
            UserName = DatabaseInitializer.AdminUserName
        });
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthProtection.RateLimitPolicy)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Identifier)
                ?? await _userManager.FindByNameAsync(request.Identifier);
            if (user == null)
            {
                _logger.LogWarning("Login attempt failed: User not found {Identifier}", request.Identifier);
                return Unauthorized(InvalidCredentialsMessage);
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Login attempt rejected: Account locked out {Identifier}", request.Identifier);
                return AccountLocked();
            }

            var result = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!result)
            {
                // Counts the failure and locks the account once the configured limit is reached
                await _userManager.AccessFailedAsync(user);
                if (await _userManager.IsLockedOutAsync(user))
                {
                    _logger.LogWarning("Account locked out after too many failed login attempts {Identifier}", request.Identifier);
                    return AccountLocked();
                }

                _logger.LogWarning("Login attempt failed: Invalid password {Identifier}", request.Identifier);
                return Unauthorized(InvalidCredentialsMessage);
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var token = await _tokenService.CreateTokenAsync(user, roles);

            _logger.LogInformation("User logged in: {Identifier}", request.Identifier);
            return Ok(new AuthResponse
            {
                Token = token,
                Email = user.Email,
                UserName = user.UserName!
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {Identifier}", request.Identifier);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: UnexpectedLoginErrorMessage);
        }
    }

    private ObjectResult AccountLocked()
    {
        return StatusCode(StatusCodes.Status429TooManyRequests, new { message = AccountLockedMessage });
    }

    [HttpPost("setup-admin")]
    [EnableRateLimiting(AuthProtection.RateLimitPolicy)]
    public async Task<ActionResult<AuthResponse>> SetupAdmin([FromBody] SetupAdminRequest request)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new { message = PasswordConfirmationMismatchMessage });
        }

        var isSetupRequired = await _settingService.GetSettingAsync(DatabaseInitializer.InitialAdminSetupRequiredKey, true);
        if (!isSetupRequired)
        {
            return Conflict(new { message = InitialAdminSetupCompletedMessage });
        }

        try
        {
            var adminUser = await _userManager.FindByNameAsync(DatabaseInitializer.AdminUserName);
            if (adminUser == null)
            {
                return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: AdminUserUnavailableMessage);
            }

            if (await _userManager.HasPasswordAsync(adminUser))
            {
                return Conflict(new { message = AdminPasswordAlreadyConfiguredMessage });
            }

            var addPasswordResult = await _userManager.AddPasswordAsync(adminUser, request.Password);
            if (!addPasswordResult.Succeeded)
            {
                foreach (var error in addPasswordResult.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }

                return ValidationProblem();
            }

            await _settingService.SetSettingAsync(
                DatabaseInitializer.InitialAdminSetupRequiredKey,
                bool.FalseString.ToLowerInvariant(),
                InitialAdminSetupDescription);

            var roles = await _userManager.GetRolesAsync(adminUser);
            var token = await _tokenService.CreateTokenAsync(adminUser, roles);

            _logger.LogInformation("Initial admin setup completed successfully.");
            return Ok(new AuthResponse
            {
                Token = token,
                Email = adminUser.Email,
                UserName = adminUser.UserName!
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during initial admin setup.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: UnexpectedAdminSetupErrorMessage);
        }
    }

    [HttpPost("register")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = DatabaseInitializer.AdminRoleName)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = new AppUser { UserName = request.Email, Email = request.Email, CreatedAt = DateTime.UtcNow };
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }
                _logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                return ValidationProblem();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = await _tokenService.CreateTokenAsync(user, roles);

            _logger.LogInformation("User registered: {Email}", request.Email);
            return Ok(new AuthResponse
            {
                Token = token,
                Email = user.Email,
                UserName = user.UserName!
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration for {Email}", request.Email);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: UnexpectedRegistrationErrorMessage);
        }
    }
}
