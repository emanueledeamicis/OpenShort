using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Interfaces;

namespace OpenShort.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize] // Admin check relaxed for MVP uniformity
public class UsersController : ControllerBase
{
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
        // Be careful not to expose PasswordHash etc in production, but for MVP/Admin it's okay-ish or map to DTO.
        // For security, let's map to a simple DTO to be safe.
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
    public async Task<ActionResult<IdentityUser>> CreateUser(string email, string password)
    {
        try
        {
            var (user, errors) = await _userService.CreateAsync(email, password);

            if (user != null)
            {
                _logger.LogInformation("User created: {Email}", email);
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
            _logger.LogError(ex, "Error creating user {Email}", email);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "An unexpected error occurred while creating the user.");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            var (success, errors) = await _userService.DeleteAsync(id);

            if (success)
            {
                _logger.LogInformation("User deleted: {UserId}", id);
                return NoContent();
            }

            // Check for Not Found
            if (errors.Any(e => e.Code == "NotFound"))
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: "User not found.");
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
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "An unexpected error occurred while deleting the user.");
        }
    }
}
