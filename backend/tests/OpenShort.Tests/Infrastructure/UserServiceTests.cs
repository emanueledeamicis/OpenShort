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
        var (success, _) = await _userService.DeleteAsync(first.Id);

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
        var (success, errors) = await _userService.DeleteAsync(only.Id);

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
        var (success, errors) = await _userService.DeleteAsync(usable.Id);

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
        var (success, _) = await _userService.DeleteAsync(withoutPassword.Id);

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
        var (success, errors) = await _userService.DeleteAsync(first.Id);

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

    // A user without the Admin role, as the removed register endpoint left them, is managed like the others
    [Test]
    public async Task DisableTwoFactor_ShouldTurnItOff_ForAUserWithoutTheAdminRole()
    {
        // Arrange
        var user = await CreateUserWithoutRoleAsync("plain@example.com");
        await EnableTwoFactorAsync(user);

        // Act
        var (success, _) = await _userService.DisableTwoFactorAsync(user.Id);

        // Assert
        success.Should().BeTrue();
        (await _userManager.GetTwoFactorEnabledAsync(user)).Should().BeFalse();
    }

    [Test]
    public async Task GetAll_ShouldListTheUsersWithoutTheAdminRoleToo()
    {
        // Arrange
        var admin = await CreateAdminAsync("admin@example.com", Password);
        var plain = await CreateUserWithoutRoleAsync("plain@example.com");

        // Act
        var accounts = await _userService.GetAllAsync();

        // Assert
        accounts.Should().HaveCount(2);
        accounts.Single(account => account.User.Id == admin.Id).IsAdmin.Should().BeTrue();
        accounts.Single(account => account.User.Id == plain.Id).IsAdmin.Should().BeFalse();
    }

    [Test]
    public async Task Delete_ShouldSucceed_ForAUserWithoutTheAdminRole()
    {
        // Arrange
        await CreateAdminAsync("admin@example.com", Password);
        var plain = await CreateUserWithoutRoleAsync("plain@example.com");

        // Act
        var (success, _) = await _userService.DeleteAsync(plain.Id);

        // Assert
        success.Should().BeTrue();
        (await _userManager.FindByIdAsync(plain.Id)).Should().BeNull();
    }

    // The last administrator is the one that cannot be deleted, whoever else can sign in
    [Test]
    public async Task Delete_ShouldFail_ForTheOnlyAdmin_WhenAUserWithoutTheRoleCanSignIn()
    {
        // Arrange
        var only = await CreateAdminAsync("admin@example.com", Password);
        await CreateUserWithoutRoleAsync("plain@example.com");

        // Act
        var (success, errors) = await _userService.DeleteAsync(only.Id);

        // Assert
        success.Should().BeFalse();
        errors.Should().ContainSingle(error => error.Code == LastAdminDeletionForbiddenErrorCode);
    }

    private async Task<AppUser> CreateUserWithoutRoleAsync(string userName)
    {
        var user = new AppUser { UserName = userName, CreatedAt = DateTime.UtcNow };
        (await _userManager.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        return user;
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
