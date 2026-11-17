using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenShort.Api.Auth;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Infrastructure;

/// <summary>
/// Admin password recovery tests. Each call to <see cref="StartAsync"/> behaves like an application start:
/// it runs the real initializer, with the real migrations and Identity configuration, on a database that
/// lives as long as the test.
/// </summary>
[TestFixture]
public class DatabaseInitializerTests
{
    private const string ResetVariable = "ADMIN_PASSWORD_RESET";
    private const string CurrentPassword = "CurrentPass123!";
    private const string NewPassword = "Temporary-Passw0rd";
    private const string OtherAdminEmail = "owner@example.com";
    private const string OtherAdminPassword = "OwnerPass123!";

    private SqliteConnection _connection = null!;
    private RecordingLoggerProvider _loggerProvider = null!;

    [SetUp]
    public void Setup()
    {
        // An in-memory SQLite database exists for as long as its connection stays open
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _loggerProvider = new RecordingLoggerProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _loggerProvider.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Initialize_ShouldRequireSetup_WhenAdminHasNoPassword()
    {
        // Act
        await StartAsync(resetPassword: null);

        // Assert
        (await IsSetupRequiredAsync()).Should().BeTrue();
        (await AdminHasPasswordAsync()).Should().BeFalse();
    }

    [Test]
    public async Task Initialize_ShouldReplacePassword_WhenResetPasswordIsValid()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);

        // Act
        await StartAsync(NewPassword);

        // Assert
        (await CheckAdminPasswordAsync(NewPassword)).Should().BeTrue();
        (await CheckAdminPasswordAsync(CurrentPassword)).Should().BeFalse();
        (await IsSetupRequiredAsync()).Should().BeFalse();
    }

    [Test]
    public async Task Initialize_ShouldSetPasswordAndCloseSetup_WhenResetPasswordIsValidAndAdminHasNoPassword()
    {
        // Arrange
        await StartAsync(resetPassword: null);

        // Act
        await StartAsync(NewPassword);

        // Assert
        (await CheckAdminPasswordAsync(NewPassword)).Should().BeTrue();
        (await IsSetupRequiredAsync()).Should().BeFalse();
    }

    [Test]
    public async Task Initialize_ShouldChangeSecurityStamp_WhenResetPasswordIsValid()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        var stampBefore = await GetAdminSecurityStampAsync();

        // Act
        await StartAsync(NewPassword);

        // Assert
        (await GetAdminSecurityStampAsync()).Should().NotBe(stampBefore);
    }

    // One case per rule of the password policy, plus the example the README used to suggest
    [TestCase("Aa1!aaa", TestName = "{m}(Too short)")]
    [TestCase("lowercase-passw0rd", TestName = "{m}(No uppercase letter)")]
    [TestCase("UPPERCASE-PASSW0RD", TestName = "{m}(No lowercase letter)")]
    [TestCase("Temporary-Password", TestName = "{m}(No digit)")]
    [TestCase("TemporaryPassw0rd", TestName = "{m}(No symbol)")]
    [TestCase("temporary_emergency_password", TestName = "{m}(Former README example)")]
    public async Task Initialize_ShouldKeepCurrentPassword_WhenResetPasswordIsInvalid(string invalidPassword)
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);

        // Act
        await StartAsync(invalidPassword);

        // Assert
        (await CheckAdminPasswordAsync(CurrentPassword)).Should().BeTrue();
        (await CheckAdminPasswordAsync(invalidPassword)).Should().BeFalse();
        (await IsSetupRequiredAsync()).Should().BeFalse();
    }

    [Test]
    public async Task Initialize_ShouldNotReopenSetup_WhenResetPasswordIsInvalidAndApplicationRestarts()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        await StartAsync("temporary_emergency_password");

        // Act: the next start, with the variable still set and then removed
        await StartAsync("temporary_emergency_password");
        await StartAsync(resetPassword: null);

        // Assert
        (await IsSetupRequiredAsync()).Should().BeFalse();
        (await CheckAdminPasswordAsync(CurrentPassword)).Should().BeTrue();
    }

    [Test]
    public async Task Initialize_ShouldLogErrorWithUnmetRules_WhenResetPasswordIsInvalid()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        _loggerProvider.Entries.Clear();

        // Act
        await StartAsync("temporary_emergency_password");

        // Assert
        var error = _loggerProvider.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Which;
        error.Message.Should().Contain(ResetVariable);
        error.Message.Should().Contain("unchanged");
        error.Message.Should().Contain("uppercase");
        error.Message.Should().Contain("digit");
        error.Message.Should().NotContain("temporary_emergency_password");
    }

    [Test]
    public async Task Initialize_ShouldNotLogThePassword_WhenResetPasswordIsValid()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        _loggerProvider.Entries.Clear();

        // Act
        await StartAsync(NewPassword);

        // Assert
        _loggerProvider.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
        _loggerProvider.Entries.Should().NotContain(entry => entry.Message.Contains(NewPassword));
    }

    [Test]
    public async Task Initialize_ShouldKeepSetupRequired_WhenResetPasswordIsInvalidAndAdminHasNoPassword()
    {
        // Arrange
        await StartAsync(resetPassword: null);

        // Act
        await StartAsync("temporary_emergency_password");

        // Assert
        (await AdminHasPasswordAsync()).Should().BeFalse();
        (await IsSetupRequiredAsync()).Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Initialize_ShouldKeepCurrentPassword_WhenResetPasswordIsBlank(string blankPassword)
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);

        // Act
        await StartAsync(blankPassword);

        // Assert
        (await CheckAdminPasswordAsync(CurrentPassword)).Should().BeTrue();
        (await IsSetupRequiredAsync()).Should().BeFalse();
    }

    [TestCase(" Temporary-Passw0rd", TestName = "{m}(Leading space)")]
    [TestCase("Temporary-Passw0rd ", TestName = "{m}(Trailing space)")]
    public async Task Initialize_ShouldKeepCurrentPasswordAndLogError_WhenResetPasswordHasSurroundingSpaces(string paddedPassword)
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        _loggerProvider.Entries.Clear();

        // Act
        await StartAsync(paddedPassword);

        // Assert
        (await CheckAdminPasswordAsync(CurrentPassword)).Should().BeTrue();
        (await CheckAdminPasswordAsync(paddedPassword)).Should().BeFalse();
        (await CheckAdminPasswordAsync(paddedPassword.Trim())).Should().BeFalse();
        var error = _loggerProvider.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Which;
        error.Message.Should().Contain(ResetVariable);
        error.Message.Should().Contain("space");
        error.Message.Should().NotContain(paddedPassword.Trim());
    }

    [Test]
    public async Task Initialize_ShouldKeepCurrentPasswordAndLogError_WhenSavingTheNewPasswordFails()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        var stampBefore = await GetAdminSecurityStampAsync();
        _loggerProvider.Entries.Clear();

        // Act
        await StartAsync(NewPassword, failUserSave: true);

        // Assert
        (await CheckAdminPasswordAsync(CurrentPassword)).Should().BeTrue();
        (await CheckAdminPasswordAsync(NewPassword)).Should().BeFalse();
        (await GetAdminSecurityStampAsync()).Should().Be(stampBefore);
        (await IsSetupRequiredAsync()).Should().BeFalse();
        var error = _loggerProvider.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Which;
        error.Message.Should().Contain("unchanged");
        error.Message.Should().Contain(RejectingUserValidator.ErrorDescription);
    }

    [Test]
    public async Task Initialize_ShouldNotRecreateAdminNorReopenSetup_WhenAdminWasDeletedAndAnotherAdministratorExists()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        await CreateOtherAdminAndDeleteDefaultAdminAsync();
        _loggerProvider.Entries.Clear();

        // Act
        await StartAsync(resetPassword: null);
        await StartAsync(resetPassword: null);

        // Assert
        (await AdminExistsAsync()).Should().BeFalse();
        (await IsSetupRequiredAsync()).Should().BeFalse();
        (await CheckPasswordAsync(OtherAdminEmail, OtherAdminPassword)).Should().BeTrue();
        _loggerProvider.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
        _loggerProvider.Entries
            .Where(entry => entry.Message.Contains("default admin user was deleted and is not created again"))
            .Should().HaveCount(2);
    }

    [Test]
    public async Task Initialize_ShouldCloseSetup_WhenAdminHasNoPasswordButAnotherAdministratorHasOne()
    {
        // Arrange: the state left by the previous versions, which created the deleted admin again
        await StartWithAdminPasswordAsync(CurrentPassword);
        await CreateOtherAdminAndDeleteDefaultAdminAsync();
        await RecreateAdminWithoutPasswordAsync();

        // Act
        await StartAsync(resetPassword: null);

        // Assert
        (await IsSetupRequiredAsync()).Should().BeFalse();
        (await AdminHasPasswordAsync()).Should().BeFalse();
    }

    [Test]
    public async Task Initialize_ShouldRecreateAdminWithPassword_WhenAdminWasDeletedAndResetPasswordIsValid()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        await CreateOtherAdminAndDeleteDefaultAdminAsync();

        // Act
        await StartAsync(NewPassword);

        // Assert
        (await CheckAdminPasswordAsync(NewPassword)).Should().BeTrue();
        (await WithAdminAsync((userManager, admin) => userManager.IsInRoleAsync(admin, DatabaseInitializer.AdminRoleName))).Should().BeTrue();
        (await IsSetupRequiredAsync()).Should().BeFalse();
    }

    // A rejected recovery must not create an admin without a password, which anyone could then claim
    [TestCase("temporary_emergency_password", TestName = "{m}(Password not valid)")]
    [TestCase(" Temporary-Passw0rd", TestName = "{m}(Leading space)")]
    [TestCase("Temporary-Passw0rd ", TestName = "{m}(Trailing space)")]
    public async Task Initialize_ShouldNotRecreateAdmin_WhenAdminWasDeletedAndResetPasswordIsRejected(string rejectedPassword)
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        await CreateOtherAdminAndDeleteDefaultAdminAsync();
        _loggerProvider.Entries.Clear();

        // Act
        await StartAsync(rejectedPassword);

        // Assert
        (await AdminExistsAsync()).Should().BeFalse();
        (await IsSetupRequiredAsync()).Should().BeFalse();
        var error = _loggerProvider.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Which;
        error.Message.Should().Contain(ResetVariable);
        error.Message.Should().NotContain(rejectedPassword.Trim());
    }

    [Test]
    public async Task Initialize_ShouldNotRecreateAdmin_WhenAdminWasDeletedAndResetPasswordIsBlank()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        await CreateOtherAdminAndDeleteDefaultAdminAsync();

        // Act
        await StartAsync("   ");

        // Assert
        (await AdminExistsAsync()).Should().BeFalse();
        (await IsSetupRequiredAsync()).Should().BeFalse();
    }

    [Test]
    public async Task Initialize_ShouldRecreateAdminAtEveryStart_WhileAValidResetPasswordIsSet()
    {
        // Arrange
        await StartWithAdminPasswordAsync(CurrentPassword);
        await CreateOtherAdminAndDeleteDefaultAdminAsync();
        await StartAsync(NewPassword);
        await DeleteDefaultAdminAsync();

        // Act
        await StartAsync(NewPassword);

        // Assert
        (await CheckAdminPasswordAsync(NewPassword)).Should().BeTrue();
    }

    [Test]
    public async Task Initialize_ShouldRequireSetup_WhenTheOnlyOtherAdministratorHasNoPassword()
    {
        // Arrange: nobody can sign in, so the setup is the only way in
        await StartAsync(resetPassword: null);
        await WithScopeAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var other = new AppUser { UserName = OtherAdminEmail, Email = OtherAdminEmail, CreatedAt = DateTime.UtcNow };
            (await userManager.CreateAsync(other)).Succeeded.Should().BeTrue();
            (await userManager.AddToRoleAsync(other, DatabaseInitializer.AdminRoleName)).Succeeded.Should().BeTrue();
            var admin = await userManager.FindByNameAsync(DatabaseInitializer.AdminUserName);
            (await userManager.DeleteAsync(admin!)).Succeeded.Should().BeTrue();
            return true;
        });

        // Act
        await StartAsync(resetPassword: null);

        // Assert
        (await AdminExistsAsync()).Should().BeTrue();
        (await IsSetupRequiredAsync()).Should().BeTrue();
    }

    [Test]
    public async Task Initialize_ShouldRequireSetup_WhenAUserWithPasswordIsNotAnAdministrator()
    {
        // Arrange
        await StartAsync(resetPassword: null);
        await WithScopeAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var other = new AppUser { UserName = OtherAdminEmail, Email = OtherAdminEmail, CreatedAt = DateTime.UtcNow };
            (await userManager.CreateAsync(other, OtherAdminPassword)).Succeeded.Should().BeTrue();
            return true;
        });

        // Act
        await StartAsync(resetPassword: null);

        // Assert
        (await IsSetupRequiredAsync()).Should().BeTrue();
    }

    // Same steps as the dashboard: create a second administrator, then delete the default one
    private Task CreateOtherAdminAndDeleteDefaultAdminAsync() => WithScopeAsync(async services =>
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var userService = new UserService(userManager);
        var (otherAdmin, _) = await userService.CreateAdminAsync(OtherAdminEmail, OtherAdminPassword);
        otherAdmin.Should().NotBeNull();

        var admin = await userManager.FindByNameAsync(DatabaseInitializer.AdminUserName);
        var (deleted, _) = await userService.DeleteAdminAsync(admin!.Id);
        deleted.Should().BeTrue();
        return true;
    });

    private Task DeleteDefaultAdminAsync() => WithScopeAsync(async services =>
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByNameAsync(DatabaseInitializer.AdminUserName);
        var (deleted, _) = await new UserService(userManager).DeleteAdminAsync(admin!.Id);
        deleted.Should().BeTrue();
        return true;
    });

    private Task RecreateAdminWithoutPasswordAsync() => WithScopeAsync(async services =>
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var admin = new AppUser { UserName = DatabaseInitializer.AdminUserName, EmailConfirmed = true, CreatedAt = DateTime.UtcNow };
        (await userManager.CreateAsync(admin)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(admin, DatabaseInitializer.AdminRoleName)).Succeeded.Should().BeTrue();
        await services.GetRequiredService<ISettingService>()
            .SetSettingAsync(DatabaseInitializer.InitialAdminSetupRequiredKey, "true");
        return true;
    });

    private Task<bool> AdminExistsAsync() => WithScopeAsync(async services =>
        await services.GetRequiredService<UserManager<AppUser>>().FindByNameAsync(DatabaseInitializer.AdminUserName) != null);

    private Task<bool> CheckPasswordAsync(string userName, string password) => WithScopeAsync(async services =>
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByNameAsync(userName);
        return user != null && await userManager.CheckPasswordAsync(user, password);
    });

    private async Task StartWithAdminPasswordAsync(string password)
    {
        await StartAsync(resetPassword: null);

        // Same steps as the initial setup endpoint
        await using var provider = BuildProvider(resetPassword: null);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByNameAsync(DatabaseInitializer.AdminUserName);
        (await userManager.AddPasswordAsync(admin!, password)).Succeeded.Should().BeTrue();
        await scope.ServiceProvider.GetRequiredService<ISettingService>()
            .SetSettingAsync(DatabaseInitializer.InitialAdminSetupRequiredKey, "false");
    }

    private async Task StartAsync(string? resetPassword, bool failUserSave = false)
    {
        await using var provider = BuildProvider(resetPassword, failUserSave);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }

    private Task<bool> IsSetupRequiredAsync() => WithScopeAsync(services =>
        services.GetRequiredService<ISettingService>().GetSettingAsync(DatabaseInitializer.InitialAdminSetupRequiredKey, true));

    private Task<bool> AdminHasPasswordAsync() => WithAdminAsync((userManager, admin) => userManager.HasPasswordAsync(admin));

    private Task<bool> CheckAdminPasswordAsync(string password) =>
        WithAdminAsync((userManager, admin) => userManager.CheckPasswordAsync(admin, password));

    private Task<string> GetAdminSecurityStampAsync() =>
        WithAdminAsync((userManager, admin) => userManager.GetSecurityStampAsync(admin));

    private Task<T> WithAdminAsync<T>(Func<UserManager<AppUser>, AppUser, Task<T>> read) => WithScopeAsync(async services =>
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByNameAsync(DatabaseInitializer.AdminUserName);
        admin.Should().NotBeNull();
        return await read(userManager, admin!);
    });

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> read)
    {
        await using var provider = BuildProvider(resetPassword: null);
        using var scope = provider.CreateScope();
        return await read(scope.ServiceProvider);
    }

    // Same registrations as Program.cs for the services the initializer uses
    private ServiceProvider BuildProvider(string? resetPassword, bool failUserSave = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ResetVariable] = resetPassword })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Information).AddProvider(_loggerProvider));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<AppDbContext, SqliteDbContext>(options => options.UseSqlite(_connection));
        services.AddIdentityCore<AppUser>(AuthProtection.ConfigureIdentity)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        if (failUserSave)
        {
            services.AddScoped<IUserValidator<AppUser>, RejectingUserValidator>();
        }

        services.AddScoped<ISettingService, SettingService>();
        services.AddSingleton(new Mock<IJwtKeyProvider>().Object);
        services.AddScoped<DatabaseInitializer>();
        return services.BuildServiceProvider();
    }

    // Identity runs the user validators before every save of a user: rejecting here makes the save fail
    private sealed class RejectingUserValidator : IUserValidator<AppUser>
    {
        public const string ErrorDescription = "Save rejected by the test.";

        public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user) =>
            Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "TestFailure", Description = ErrorDescription }));
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(string categoryName, List<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            // Only the initializer is recorded: Entity Framework logs its own commands
            public bool IsEnabled(LogLevel logLevel) => categoryName == typeof(DatabaseInitializer).FullName;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    entries.Add(new LogEntry(logLevel, formatter(state, exception)));
                }
            }
        }
    }
}
