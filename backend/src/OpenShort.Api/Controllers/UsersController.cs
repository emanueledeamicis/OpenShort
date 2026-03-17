using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = DatabaseInitializer.AdminRoleName)]
public class UsersController : ControllerBase
{
    private const string UserNotFoundErrorCode = "NotFound";
    private const string LastAdminDeletionForbiddenErrorCode = "LastAdminDeletionForbidden";
    private const string UserNotFoundMessage = "User not found.";
    private const string LastAdminDeletionForbiddenMessage = "You cannot delete the last remaining admin user.";
    private const string CurrentUserDeletionForbiddenMessage = "You cannot delete the currently signed-in user.";
    private const string UnexpectedUserCreateErrorMessage = "An unexpected error occurred while creating the user.";
    private const string UnexpectedUserDeleteErrorMessage = "An unexpected error occurred while deleting the user.";

    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminUserDto>>> GetUsers()
    {
        var users = await _userService.GetAllAdminsAsync();
        var userDtos = users.Select(u => new AdminUserDto
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
            Email = u.Email,
            CreatedAt = u.CreatedAt
        });
        
        return Ok(userDtos);
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var (user, errors) = await _userService.CreateAdminAsync(request.Email, request.Password);

            if (user != null)
            {
                _logger.LogInformation("User created: {Email}", request.Email);
                return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new AdminUserDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email,
                    CreatedAt = user.CreatedAt
                });
            }

            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }
            return ValidationProblem();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email}", request.Email);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: UnexpectedUserCreateErrorMessage);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.Equals(currentUserId, id, StringComparison.Ordinal))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: CurrentUserDeletionForbiddenMessage);
            }

            var (success, errors) = await _userService.DeleteAdminAsync(id);

            if (success)
            {
                _logger.LogInformation("User deleted: {UserId}", id);
                return NoContent();
            }

            if (errors.Any(e => e.Code == UserNotFoundErrorCode))
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: UserNotFoundMessage);
            }

            if (errors.Any(e => e.Code == LastAdminDeletionForbiddenErrorCode))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: LastAdminDeletionForbiddenMessage);
            }

            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }
            return ValidationProblem();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: UnexpectedUserDeleteErrorMessage);
        }
    }
}

public class AdminUserDto
{
    public required string Id { get; set; }
    public required string UserName { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
