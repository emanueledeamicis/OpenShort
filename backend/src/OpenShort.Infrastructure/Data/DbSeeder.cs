using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;

namespace OpenShort.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, UserManager<IdentityUser> userManager)
    {
        // Ensure Database is created
        await context.Database.EnsureCreatedAsync();

        // Seed Default Domain (localhost)
        var defaultDomain = "localhost";
        if (!await context.Domains.AnyAsync(d => d.Host == defaultDomain))
        {
            context.Domains.Add(new Domain { Host = defaultDomain, IsActive = true });
            await context.SaveChangesAsync();
        }

        // Seed Admin User
        var adminEmail = "admin@openshort.local";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(adminUser, "Admin123!");
        }
    }
}
