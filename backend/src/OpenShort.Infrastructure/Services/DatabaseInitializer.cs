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
    public const string AdminRoleName = "Admin";
    public const string InitialAdminSetupRequiredKey = "InitialAdminSetupRequired";
    private const string DefaultDomain = "localhost";
    private const string InitialAdminSetupDescription = "Indicates whether the initial admin password setup flow is still required.";
    private const string AdminPasswordResetEnvironmentVariable = "ADMIN_PASSWORD_RESET";
    private const string ApplyingMigrationsMessage = "Applying database migrations...";
    private const string MigrationsAppliedMessage = "Database migrations applied successfully.";
    private const string DatabaseConnectionCheckFailedMessage = "Database connection check failed after migrations. Skipping bootstrap initialization.";
    private const string JwtKeyBootstrapWarningMessage = "Failed to auto-generate or retrieve JWT key during bootstrap initialization.";
    private const string AdminSeedFailedMessage = "Failed to seed admin user: {Errors}";
    private const string AdminRoleSeedFailedMessage = "Failed to seed admin role: {Errors}";
    private const string AdminRoleAssignmentFailedMessage = "Failed to assign admin role to bootstrap user: {Errors}";
    private const string AdminPasswordRemoveFailedMessage = "Failed to remove current admin password during bootstrap reset: {Errors}";
    private const string AdminPasswordResetFailedMessage = "Failed to apply bootstrap admin password reset: {Errors}";
    private const string AdminPasswordResetCompletedMessage = "Bootstrap admin password reset completed successfully.";

    private readonly AppDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ISettingService _settingService;
    private readonly IConfiguration _configuration;
    private readonly IJwtKeyProvider _jwtKeyProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        AppDbContext context,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ISettingService settingService,
        IConfiguration configuration,
        IJwtKeyProvider jwtKeyProvider,
        ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _settingService = settingService;
        _configuration = configuration;
        _jwtKeyProvider = jwtKeyProvider;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation(ApplyingMigrationsMessage);
        await _context.Database.MigrateAsync();
        _logger.LogInformation(MigrationsAppliedMessage);

        if (!_context.Database.CanConnect())
        {
            _logger.LogWarning(DatabaseConnectionCheckFailedMessage);
            return;
        }

        await _context.Database.EnsureCreatedAsync();
        await EnsureJwtKeyAsync();
        await EnsureDefaultDomainAsync();
        await EnsureAdminRoleAsync();
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
            _logger.LogWarning(ex, JwtKeyBootstrapWarningMessage);
        }
    }

    private async Task EnsureDefaultDomainAsync()
    {
        if (await _context.Domains.AnyAsync(domain => domain.Host == DefaultDomain))
        {
            return;
        }

        _context.Domains.Add(new Domain
        {
            Host = DefaultDomain,
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
                _logger.LogWarning(AdminSeedFailedMessage, string.Join(", ", createResult.Errors.Select(error => error.Description)));
                return;
            }
        }

        if (!await _userManager.IsInRoleAsync(adminUser, AdminRoleName))
        {
            var roleAssignmentResult = await _userManager.AddToRoleAsync(adminUser, AdminRoleName);
            if (!roleAssignmentResult.Succeeded)
            {
                _logger.LogWarning(AdminRoleAssignmentFailedMessage, string.Join(", ", roleAssignmentResult.Errors.Select(error => error.Description)));
            }
        }

        var adminHasPassword = await _userManager.HasPasswordAsync(adminUser);
        await _settingService.SetSettingAsync(
            InitialAdminSetupRequiredKey,
            (!adminHasPassword).ToString().ToLowerInvariant(),
            InitialAdminSetupDescription);

        var resetPassword = _configuration[AdminPasswordResetEnvironmentVariable];
        if (string.IsNullOrWhiteSpace(resetPassword))
        {
            return;
        }

        if (await _userManager.HasPasswordAsync(adminUser))
        {
            var removeResult = await _userManager.RemovePasswordAsync(adminUser);
            if (!removeResult.Succeeded)
            {
                _logger.LogWarning(AdminPasswordRemoveFailedMessage, string.Join(", ", removeResult.Errors.Select(error => error.Description)));
                return;
            }
        }

        var passwordResult = await _userManager.AddPasswordAsync(adminUser, resetPassword);
        if (!passwordResult.Succeeded)
        {
            _logger.LogWarning(AdminPasswordResetFailedMessage, string.Join(", ", passwordResult.Errors.Select(error => error.Description)));
            return;
        }

        await _settingService.SetSettingAsync(
            InitialAdminSetupRequiredKey,
            bool.FalseString.ToLowerInvariant(),
            InitialAdminSetupDescription);
        _logger.LogInformation(AdminPasswordResetCompletedMessage);
    }

    private async Task EnsureAdminRoleAsync()
    {
        if (await _roleManager.RoleExistsAsync(AdminRoleName))
        {
            return;
        }

        var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(AdminRoleName));
        if (!createRoleResult.Succeeded)
        {
            _logger.LogWarning(AdminRoleSeedFailedMessage, string.Join(", ", createRoleResult.Errors.Select(error => error.Description)));
        }
    }
}
