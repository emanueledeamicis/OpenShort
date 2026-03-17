using Microsoft.AspNetCore.Identity;

namespace OpenShort.Core.Entities;

public class AppUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
