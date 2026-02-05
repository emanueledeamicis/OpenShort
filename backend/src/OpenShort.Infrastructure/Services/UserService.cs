using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Interfaces;

namespace OpenShort.Infrastructure.Services;

public class UserService : IUserService
{
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
            // If user not found, technically "Success" depends on semantics. 
            // Controller returned NotFound. 
            // Service should probably indicate "Not Found".
            // My return signature `(bool Success, IEnumerable<IdentityError> Errors)` doesn't distinguish NotFound from Error easily 
            // without errors logic.
            // Let's assume Success=false and Empty Errors implies Not Found? Or specific error?
            // "User not found" is reasonable.
            return (false, new [] { new IdentityError { Code = "NotFound", Description = "User not found." } });
        }

        var result = await _userManager.DeleteAsync(user);
        
        return (result.Succeeded, result.Errors);
    }
}
