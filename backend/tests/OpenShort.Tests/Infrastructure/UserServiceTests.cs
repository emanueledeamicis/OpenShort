using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenShort.Api.Auth;
using OpenShort.Core.Entities;
using OpenShort.Infrastructure.Data;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Infrastructure;

/// <summary>
/// Admin deletion tests running against the real Identity UserManager, configured as in the application.
/// </summary>
[TestFixture]
public class UserServiceTests
{
    private const string Password = "StrongPass123!";
    private const string LastAdminDeletionForbiddenErrorCode = "LastAdminDeletionForbidden";

    private ServiceProvider _serviceProvider = null!;
    private UserManager<AppUser> _userManager = null!;
    private UserService _userService = null!;

    [SetUp]
    public async Task Setup()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentityCore<AppUser>(AuthProtection.ConfigureIdentity)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        _serviceProvider = services.BuildServiceProvider();
        _userManager = _serviceProvider.GetRequiredService<UserManager<AppUser>>();
        _userService = new UserService(_userManager);

        var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        (await roleManager.CreateAsync(new IdentityRole(DatabaseInitializer.AdminRoleName))).Succeeded.Should().BeTrue();
    }

    [TearDown]
    public void TearDown()
    {
        _userManager.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task DeleteAdmin_ShouldSucceed_WhenAnotherAdminCanSignIn()
    {
        // Arrange
        var first = await CreateAdminAsync("first@example.com", Password);
        await CreateAdminAsync("second@example.com", Password);

        // Act
        var (success, _) = await _userService.DeleteAdminAsync(first.Id);

        // Assert
        success.Should().BeTrue();
        (await _userManager.FindByIdAsync(first.Id)).Should().BeNull();
    }

    [Test]
    public async Task DeleteAdmin_ShouldFail_WhenItIsTheOnlyAdmin()
    {
        // Arrange
        var only = await CreateAdminAsync("only@example.com", Password);

        // Act
        var (success, errors) = await _userService.DeleteAdminAsync(only.Id);

        // Assert
        success.Should().BeFalse();
        errors.Should().ContainSingle(error => error.Code == LastAdminDeletionForbiddenErrorCode);
        (await _userManager.FindByIdAsync(only.Id)).Should().NotBeNull();
    }

    [Test]
    public async Task DeleteAdmin_ShouldFail_WhenTheOnlyOtherAdminHasNoPassword()
    {
        // Arrange: the other admin cannot sign in, so deleting this one would leave nobody able to
        var usable = await CreateAdminAsync("owner@example.com", Password);
        await CreateAdminAsync(DatabaseInitializer.AdminUserName, password: null);

        // Act
        var (success, errors) = await _userService.DeleteAdminAsync(usable.Id);

        // Assert
        success.Should().BeFalse();
        errors.Should().ContainSingle(error => error.Code == LastAdminDeletionForbiddenErrorCode);
        (await _userManager.FindByIdAsync(usable.Id)).Should().NotBeNull();
    }

    [Test]
    public async Task DeleteAdmin_ShouldSucceed_ForAnAdminWithoutPassword_WhenAnotherAdminCanSignIn()
    {
        // Arrange
        await CreateAdminAsync("owner@example.com", Password);
        var withoutPassword = await CreateAdminAsync(DatabaseInitializer.AdminUserName, password: null);

        // Act
        var (success, _) = await _userService.DeleteAdminAsync(withoutPassword.Id);

        // Assert
        success.Should().BeTrue();
        (await _userManager.FindByIdAsync(withoutPassword.Id)).Should().BeNull();
    }

    [Test]
    public async Task DeleteAdmin_ShouldFail_ForAnAdminWithoutPassword_WhenNoAdminCanSignIn()
    {
        // Arrange
        var first = await CreateAdminAsync("first@example.com", password: null);
        await CreateAdminAsync("second@example.com", password: null);

        // Act
        var (success, errors) = await _userService.DeleteAdminAsync(first.Id);

        // Assert
        success.Should().BeFalse();
        errors.Should().ContainSingle(error => error.Code == LastAdminDeletionForbiddenErrorCode);
    }

    [Test]
    public async Task DisableTwoFactor_ShouldTurnItOffAndRemoveTheSecretAndTheRecoveryCodes()
    {
        // Arrange
        var user = await CreateAdminAsync("owner@example.com", Password);
        await EnableTwoFactorAsync(user);

        // Act
        var (success, _) = await _userService.DisableTwoFactorAsync(user.Id);

        // Assert
        success.Should().BeTrue();
        var saved = await _userManager.FindByIdAsync(user.Id);
        (await _userManager.GetTwoFactorEnabledAsync(saved!)).Should().BeFalse();
        (await _userManager.GetAuthenticatorKeyAsync(saved!)).Should().BeNull();
        (await _userManager.CountRecoveryCodesAsync(saved!)).Should().Be(0);
        (await _userManager.CheckPasswordAsync(saved!, Password)).Should().BeTrue();
    }

    [Test]
    public async Task DisableTwoFactor_ShouldNotTouchAnotherUser()
    {
        // Arrange
        var user = await CreateAdminAsync("owner@example.com", Password);
        var other = await CreateAdminAsync("other@example.com", Password);
        await EnableTwoFactorAsync(user);
        await EnableTwoFactorAsync(other);

        // Act
        await _userService.DisableTwoFactorAsync(user.Id);

        // Assert
        (await _userManager.GetTwoFactorEnabledAsync(other)).Should().BeTrue();
        (await _userManager.GetAuthenticatorKeyAsync(other)).Should().NotBeNull();
        (await _userManager.CountRecoveryCodesAsync(other)).Should().Be(3);
    }

    [Test]
    public async Task DisableTwoFactor_ShouldFail_WhenItIsNotEnabled()
    {
        // Arrange
        var user = await CreateAdminAsync("owner@example.com", Password);

        // Act
        var (success, errors) = await _userService.DisableTwoFactorAsync(user.Id);

        // Assert
        success.Should().BeFalse();
        errors.Should().ContainSingle(error => error.Code == UserService.TwoFactorNotEnabledErrorCode);
    }

    [Test]
    public async Task DisableTwoFactor_ShouldFail_WhenTheUserDoesNotExist()
    {
        // Act
        var (success, errors) = await _userService.DisableTwoFactorAsync("missing-id");

        // Assert
        success.Should().BeFalse();
        errors.Should().ContainSingle(error => error.Code == "NotFound");
    }

    [Test]
    public async Task DisableTwoFactor_ShouldFail_WhenTheUserIsNotAnAdministrator()
    {
        // Arrange: the users endpoints manage administrators only
        var user = new AppUser { UserName = "plain@example.com", CreatedAt = DateTime.UtcNow };
        (await _userManager.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        await EnableTwoFactorAsync(user);

        // Act
        var (success, errors) = await _userService.DisableTwoFactorAsync(user.Id);

        // Assert
        success.Should().BeFalse();
        errors.Should().ContainSingle(error => error.Code == "NotFound");
        (await _userManager.GetTwoFactorEnabledAsync(user)).Should().BeTrue();
    }

    private async Task EnableTwoFactorAsync(AppUser user)
    {
        (await _userManager.ResetAuthenticatorKeyAsync(user)).Succeeded.Should().BeTrue();
        (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 3)).Should().HaveCount(3);
        (await _userManager.SetTwoFactorEnabledAsync(user, true)).Succeeded.Should().BeTrue();
    }

    private async Task<AppUser> CreateAdminAsync(string userName, string? password)
    {
        var user = new AppUser { UserName = userName, CreatedAt = DateTime.UtcNow };
        var createResult = password == null
            ? await _userManager.CreateAsync(user)
            : await _userManager.CreateAsync(user, password);
        createResult.Succeeded.Should().BeTrue();
        (await _userManager.AddToRoleAsync(user, DatabaseInitializer.AdminRoleName)).Succeeded.Should().BeTrue();
        return user;
    }
}
