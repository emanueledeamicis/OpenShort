using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private const string UserNotFoundMessage = "User not found.";
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
    public async Task<ActionResult<IEnumerable<dynamic>>> GetUsers()
    {
        var users = await _userService.GetAllAsync();
        var userDtos = users.Select(u => new 
        {
            u.Id,
            u.UserName,
            u.Email,
            u.EmailConfirmed
        });
        
        return Ok(userDtos);
    }

    [HttpPost]
    public async Task<ActionResult<IdentityUser>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var (user, errors) = await _userService.CreateAsync(request.Email, request.Password);

            if (user != null)
            {
                _logger.LogInformation("User created: {Email}", request.Email);
                return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new { user.Id, user.UserName, user.Email });
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

            var (success, errors) = await _userService.DeleteAsync(id);

            if (success)
            {
                _logger.LogInformation("User deleted: {UserId}", id);
                return NoContent();
            }

            if (errors.Any(e => e.Code == UserNotFoundErrorCode))
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: UserNotFoundMessage);
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

public class CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
