using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using OpenShort.Core.Entities;

namespace OpenShort.Api.Auth;

/// <summary>
/// Checks of a dashboard token that its signature cannot answer: a token lasts for days,
/// the account it was issued to may be gone in the meantime.
/// </summary>
public static class SessionValidation
{
    private const string UserDeletedMessage = "The user of this token no longer exists.";

    /// <summary>
    /// Rejects the token of a user who was deleted, so that deleting an account also ends its sessions.
    /// It reads the user on every request made with a token: redirects carry none, and the controllers
    /// that load the same user afterwards get it without a second query.
    /// </summary>
    public static async Task RejectDeletedUserAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();

        if (string.IsNullOrEmpty(userId) || await userManager.FindByIdAsync(userId) == null)
        {
            context.Fail(UserDeletedMessage);
        }
    }
}
