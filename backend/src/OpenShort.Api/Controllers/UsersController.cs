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

    public UsersController(IUserService userService)
    {
        _userService = userService;
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
        var (user, errors) = await _userService.CreateAsync(email, password);

        if (user != null)
        {
             return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new { user.Id, user.UserName, user.Email });
        }

        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }
        return ValidationProblem();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var (success, errors) = await _userService.DeleteAsync(id);

        if (success)
        {
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
}
