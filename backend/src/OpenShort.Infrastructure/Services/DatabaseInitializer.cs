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
    public const string CacheDurationSecondsKey = "CacheDurationSeconds";
    private const string DefaultDomain = "localhost";
    private const string DefaultCacheDurationSecondsValue = "600";
    private const string InitialAdminSetupDescription = "Indicates whether the initial admin password setup flow is still required.";
    public const string CacheDurationSecondsDescription = "Duration in seconds for which redirects are cached in memory.";
    private const string AdminPasswordResetEnvironmentVariable = "ADMIN_PASSWORD_RESET";
    private const string ApplyingMigrationsMessage = "Applying database migrations...";
    private const string MigrationsAppliedMessage = "Database migrations applied successfully.";
    private const string DatabaseConnectionCheckFailedMessage = "Database connection check failed after migrations. Skipping bootstrap initialization.";
    private const string JwtKeyBootstrapWarningMessage = "Failed to auto-generate or retrieve JWT key during bootstrap initialization.";
    private const string AdminSeedFailedMessage = "Failed to seed admin user: {Errors}";
    private const string AdminRoleSeedFailedMessage = "Failed to seed admin role: {Errors}";
    private const string AdminRoleAssignmentFailedMessage = "Failed to assign admin role to bootstrap user: {Errors}";
    private const string AdminPasswordResetSurroundingSpacesMessage = "{Variable} was not applied because the value starts or ends with a space, the admin account is unchanged. Remove the spaces around the value.";
    private const string AdminPasswordResetRejectedMessage = "{Variable} was not applied because the new password is not valid, the admin account is unchanged: {Errors}";
    private const string AdminPasswordResetFailedMessage = "Failed to apply bootstrap admin password reset, the admin password is unchanged: {Errors}";
    private const string AdminPasswordResetCompletedMessage = "Bootstrap admin password reset completed successfully.";
    private const string AdminUserNotRecreatedMessage = "The default admin user was deleted and is not created again: another administrator can sign in.";

    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ISettingService _settingService;
    private readonly IConfiguration _configuration;
    private readonly IJwtKeyProvider _jwtKeyProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        AppDbContext context,
        UserManager<AppUser> userManager,
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
        await EnsureDefaultSettingsAsync();
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

    private async Task EnsureDefaultSettingsAsync()
    {
        if (await _settingService.GetSettingAsync(CacheDurationSecondsKey) == null)
        {
            await _settingService.SetSettingAsync(
                CacheDurationSecondsKey,
                DefaultCacheDurationSecondsValue,
                CacheDurationSecondsDescription);
        }
    }

    private async Task EnsureAdminUserAsync()
    {
        var resetPassword = _configuration[AdminPasswordResetEnvironmentVariable];
        var adminUser = await _userManager.FindByNameAsync(AdminUserName);

        // Checked before the account is created or touched: a rejected password must not leave
        // an admin without a password behind
        var applyPasswordReset = !string.IsNullOrWhiteSpace(resetPassword)
            && await IsResetPasswordValidAsync(adminUser ?? new AppUser { UserName = AdminUserName }, resetPassword);

        if (adminUser == null)
        {
            // The default admin can be deleted when another administrator exists. Creating it again
            // without a password would leave an account anyone could claim through the initial setup.
            // It comes back only on request, through a valid password recovery.
            if (!applyPasswordReset && await AdminAccounts.AnyAbleToSignInAsync(_userManager))
            {
                await SetInitialAdminSetupRequiredAsync(false);
                _logger.LogInformation(AdminUserNotRecreatedMessage);
                return;
            }

            adminUser = new AppUser
            {
                UserName = AdminUserName,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
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

        // The setup is for an instance nobody can sign in to: with any usable administrator it stays closed
        await SetInitialAdminSetupRequiredAsync(!await AdminAccounts.AnyAbleToSignInAsync(_userManager));

        if (!applyPasswordReset)
        {
            return;
        }

        // The new hash replaces the old one in a single save, so there is no moment without a password
        var previousPasswordHash = adminUser.PasswordHash;
        adminUser.PasswordHash = _userManager.PasswordHasher.HashPassword(adminUser, resetPassword!);
        var passwordResult = await _userManager.UpdateSecurityStampAsync(adminUser);
        if (!passwordResult.Succeeded)
        {
            adminUser.PasswordHash = previousPasswordHash;
            _logger.LogError(AdminPasswordResetFailedMessage, string.Join(", ", passwordResult.Errors.Select(error => error.Description)));
            return;
        }

        await SetInitialAdminSetupRequiredAsync(false);
        _logger.LogInformation(AdminPasswordResetCompletedMessage);
    }

    private async Task<bool> IsResetPasswordValidAsync(AppUser adminUser, string resetPassword)
    {
        // Surrounding spaces would become part of the password without anyone noticing
        if (resetPassword != resetPassword.Trim())
        {
            _logger.LogError(AdminPasswordResetSurroundingSpacesMessage, AdminPasswordResetEnvironmentVariable);
            return false;
        }

        var validationErrors = new List<IdentityError>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, adminUser, resetPassword);
            validationErrors.AddRange(validationResult.Errors);
        }

        if (validationErrors.Count > 0)
        {
            _logger.LogError(
                AdminPasswordResetRejectedMessage,
                AdminPasswordResetEnvironmentVariable,
                string.Join(" ", validationErrors.Select(error => error.Description)));
            return false;
        }

        return true;
    }

    private Task SetInitialAdminSetupRequiredAsync(bool isRequired) =>
        _settingService.SetSettingAsync(
            InitialAdminSetupRequiredKey,
            isRequired.ToString().ToLowerInvariant(),
            InitialAdminSetupDescription);

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
