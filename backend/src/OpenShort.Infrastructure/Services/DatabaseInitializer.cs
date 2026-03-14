using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Infrastructure.Services;

public class DatabaseInitializer
{
    public const string AdminUserName = "admin";
    public const string InitialAdminSetupRequiredKey = "InitialAdminSetupRequired";

    private readonly AppDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ISettingService _settingService;
    private readonly IConfiguration _configuration;
    private readonly IJwtKeyProvider _jwtKeyProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        AppDbContext context,
        UserManager<IdentityUser> userManager,
        ISettingService settingService,
        IConfiguration configuration,
        IJwtKeyProvider jwtKeyProvider,
        ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _userManager = userManager;
        _settingService = settingService;
        _configuration = configuration;
        _jwtKeyProvider = jwtKeyProvider;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Applying database migrations...");
        await _context.Database.MigrateAsync();
        _logger.LogInformation("Database migrations applied successfully.");

        if (!_context.Database.CanConnect())
        {
            _logger.LogWarning("Database connection check failed after migrations. Skipping bootstrap initialization.");
            return;
        }

        await _context.Database.EnsureCreatedAsync();
        await EnsureJwtKeyAsync();
        await EnsureDefaultDomainAsync();
        await EnsureAdminUserAsync();
    }

    private async Task EnsureJwtKeyAsync()
    {
        try
        {
            await _jwtKeyProvider.GetOrGenerateKeyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-generate or retrieve JWT key during bootstrap initialization.");
        }
    }

    private async Task EnsureDefaultDomainAsync()
    {
        const string defaultDomain = "localhost";
        if (await _context.Domains.AnyAsync(domain => domain.Host == defaultDomain))
        {
            return;
        }

        _context.Domains.Add(new Domain
        {
            Host = defaultDomain,
            IsActive = true
        });
        await _context.SaveChangesAsync();
    }

    private async Task EnsureAdminUserAsync()
    {
        var adminUser = await _userManager.FindByNameAsync(AdminUserName);
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = AdminUserName,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(adminUser);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning("Failed to seed admin user: {Errors}", string.Join(", ", createResult.Errors.Select(error => error.Description)));
                return;
            }
        }

        var adminHasPassword = await _userManager.HasPasswordAsync(adminUser);
        await _settingService.SetSettingAsync(
            InitialAdminSetupRequiredKey,
            (!adminHasPassword).ToString().ToLowerInvariant(),
            "Indicates whether the initial admin password setup flow is still required.");

        var resetPassword = _configuration["ADMIN_PASSWORD_RESET"];
        if (string.IsNullOrWhiteSpace(resetPassword))
        {
            return;
        }

        if (await _userManager.HasPasswordAsync(adminUser))
        {
            var removeResult = await _userManager.RemovePasswordAsync(adminUser);
            if (!removeResult.Succeeded)
            {
                _logger.LogWarning("Failed to remove current admin password during bootstrap reset: {Errors}", string.Join(", ", removeResult.Errors.Select(error => error.Description)));
                return;
            }
        }

        var passwordResult = await _userManager.AddPasswordAsync(adminUser, resetPassword);
        if (!passwordResult.Succeeded)
        {
            _logger.LogWarning("Failed to apply bootstrap admin password reset: {Errors}", string.Join(", ", passwordResult.Errors.Select(error => error.Description)));
            return;
        }

        await _settingService.SetSettingAsync(
            InitialAdminSetupRequiredKey,
            bool.FalseString.ToLowerInvariant(),
            "Indicates whether the initial admin password setup flow is still required.");
        _logger.LogInformation("Bootstrap admin password reset completed successfully.");
    }
}
