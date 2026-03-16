using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Interfaces;

namespace OpenShort.Infrastructure.Services;

public class UserService : IUserService
{
    private const string UserNotFoundErrorCode = "NotFound";
    private const string UserNotFoundErrorDescription = "User not found.";

    private readonly UserManager<IdentityUser> _userManager;

    public UserService(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IEnumerable<IdentityUser>> GetAllAsync()
    {
        return await _userManager.Users.ToListAsync();
    }

    public async Task<IdentityUser?> GetByIdAsync(string id)
    {
        return await _userManager.FindByIdAsync(id);
    }

    public async Task<(IdentityUser? User, IEnumerable<IdentityError> Errors)> CreateAsync(string email, string password)
    {
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            return (user, Enumerable.Empty<IdentityError>());
        }

        return (null, result.Errors);
    }

    public async Task<(bool Success, IEnumerable<IdentityError> Errors)> DeleteAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return (false, new []
            {
                new IdentityError
                {
                    Code = UserNotFoundErrorCode,
                    Description = UserNotFoundErrorDescription
                }
            });
        }

        var result = await _userManager.DeleteAsync(user);
        
        return (result.Succeeded, result.Errors);
    }
}
