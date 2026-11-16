using System.Globalization;
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
    private const string LoginDelayedMessageFormat = "Too many failed login attempts. Try again in {0} second(s).";
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
    private readonly LoginThrottle _loginThrottle;
    private readonly ISettingService _settingService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        LoginThrottle loginThrottle,
        ISettingService settingService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _loginThrottle = loginThrottle;
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
            AppUser? user = null;
            var clientAddress = ClientAddress.GetPartitionKey(HttpContext);

            // Unknown identifiers are throttled like existing accounts, so the delay does not reveal which accounts exist
            var attempt = await _loginThrottle.AttemptAsync(
                request.Identifier,
                clientAddress,
                async () =>
                {
                    user = await _userManager.FindByEmailAsync(request.Identifier)
                        ?? await _userManager.FindByNameAsync(request.Identifier);
                    return user != null && await _userManager.CheckPasswordAsync(user, request.Password);
                },
                HttpContext.RequestAborted);

            if (attempt.RetryAfter is { } retryAfter)
            {
                var retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                _logger.LogWarning("Login attempt delayed after repeated failures {Identifier}", request.Identifier);
                Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new { message = string.Format(CultureInfo.InvariantCulture, LoginDelayedMessageFormat, retryAfterSeconds) });
            }

            if (!attempt.Succeeded || user == null)
            {
                _logger.LogWarning("Login attempt failed: Invalid credentials {Identifier}", request.Identifier);
                return Unauthorized(InvalidCredentialsMessage);
            }

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

            _loginThrottle.Reset(DatabaseInitializer.AdminUserName, ClientAddress.GetPartitionKey(HttpContext));

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
