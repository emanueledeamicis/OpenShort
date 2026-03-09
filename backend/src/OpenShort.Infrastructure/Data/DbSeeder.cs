using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenShort.Core.Entities;

namespace OpenShort.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, UserManager<IdentityUser> userManager, IServiceProvider serviceProvider)
    {
        // Ensure Database is created
        await context.Database.EnsureCreatedAsync();

        try
        {
            // Seed / Generazione JWT Key
            var jwtProvider = serviceProvider.GetRequiredService<OpenShort.Infrastructure.Services.IJwtKeyProvider>();
            await jwtProvider.GetOrGenerateKeyAsync();
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<AppDbContext>>();
            logger.LogWarning(ex, "Failed to auto-generate or retrieve JWT key during DB Seeding.");
        }

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
