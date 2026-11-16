using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenShort.Api.Auth;
using OpenShort.Api.Controllers;
using OpenShort.Api.Models;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Tests.Api;

/// <summary>
/// Login tests running against the real Identity UserManager, configured as in the application.
/// </summary>
[TestFixture]
public class AuthControllerLoginTests
{
    private const string UserName = "admin";
    private const string Password = "StrongPass123!";
    private const string WrongPassword = "WrongPass123!";

    private ServiceProvider _serviceProvider = null!;
    private UserManager<AppUser> _userManager = null!;
    private Mock<ITokenService> _tokenServiceMock = null!;
    private AuthController _controller = null!;

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

        var createResult = await _userManager.CreateAsync(
            new AppUser { UserName = UserName, CreatedAt = DateTime.UtcNow },
            Password);
        createResult.Succeeded.Should().BeTrue();

        _tokenServiceMock = new Mock<ITokenService>();
        _tokenServiceMock
            .Setup(service => service.CreateTokenAsync(It.IsAny<AppUser>(), It.IsAny<IList<string>>()))
            .ReturnsAsync("token-value");

        _controller = new AuthController(
            _userManager,
            _tokenServiceMock.Object,
            new Mock<ISettingService>().Object,
            new Mock<ILogger<AuthController>>().Object);
    }

    [TearDown]
    public void TearDown()
    {
        _userManager.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task Login_ShouldReturnToken_WhenCredentialsAreValid()
    {
        // Act
        var result = await LoginAsync(Password);

        // Assert
        var payload = result.Result.Should().BeOfType<OkObjectResult>().Which.Value as AuthResponse;
        payload.Should().NotBeNull();
        payload!.Token.Should().Be("token-value");
        payload.UserName.Should().Be(UserName);
    }

    [Test]
    public async Task Login_ShouldReturnUnauthorized_WhenPasswordIsWrong()
    {
        // Act
        var result = await LoginAsync(WrongPassword);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Test]
    public async Task Login_ShouldReturnUnauthorized_WhenUserDoesNotExist()
    {
        // Act
        var result = await _controller.Login(new LoginRequest { Identifier = "nobody", Password = Password });

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Test]
    public async Task Login_ShouldLockAccount_WhenFailedAttemptsReachTheLimit()
    {
        // Act
        var results = new List<ActionResult<AuthResponse>>();
        for (var attempt = 0; attempt < AuthProtection.MaxFailedAccessAttempts; attempt++)
        {
            results.Add(await LoginAsync(WrongPassword));
        }

        // Assert
        results.Take(AuthProtection.MaxFailedAccessAttempts - 1)
            .Should().AllSatisfy(result => result.Result.Should().BeOfType<UnauthorizedObjectResult>());
        results.Last().Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Test]
    public async Task Login_ShouldRejectCorrectPassword_WhileAccountIsLocked()
    {
        // Arrange
        await LockAccountAsync();

        // Act
        var result = await LoginAsync(Password);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        _tokenServiceMock.Verify(
            service => service.CreateTokenAsync(It.IsAny<AppUser>(), It.IsAny<IList<string>>()),
            Times.Never);
    }

    [Test]
    public async Task Login_ShouldSucceed_WhenLockoutHasExpired()
    {
        // Arrange
        await LockAccountAsync();
        var user = await _userManager.FindByNameAsync(UserName);
        await _userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        var result = await LoginAsync(Password);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task Login_ShouldResetFailedAttempts_WhenLoginSucceeds()
    {
        // Arrange
        for (var attempt = 0; attempt < AuthProtection.MaxFailedAccessAttempts - 1; attempt++)
        {
            await LoginAsync(WrongPassword);
        }

        // Act
        var successfulLogin = await LoginAsync(Password);
        var failedLoginAfterSuccess = await LoginAsync(WrongPassword);

        // Assert
        successfulLogin.Result.Should().BeOfType<OkObjectResult>();
        failedLoginAfterSuccess.Result.Should().BeOfType<UnauthorizedObjectResult>();
        var user = await _userManager.FindByNameAsync(UserName);
        (await _userManager.GetAccessFailedCountAsync(user!)).Should().Be(1);
    }

    [Test]
    public void ConfigureIdentity_ShouldSetLockoutAndPasswordPolicy()
    {
        // Act
        var options = new IdentityOptions();
        AuthProtection.ConfigureIdentity(options);

        // Assert
        options.Lockout.AllowedForNewUsers.Should().BeTrue();
        options.Lockout.MaxFailedAccessAttempts.Should().Be(5);
        options.Lockout.DefaultLockoutTimeSpan.Should().Be(TimeSpan.FromMinutes(5));
        options.Password.RequiredLength.Should().Be(8);
        options.Password.RequireDigit.Should().BeTrue();
        options.Password.RequireLowercase.Should().BeTrue();
        options.Password.RequireUppercase.Should().BeTrue();
        options.Password.RequireNonAlphanumeric.Should().BeTrue();
    }

    [TestCase("Short1!")]
    [TestCase("nouppercase123!")]
    [TestCase("NoDigitsHere!")]
    [TestCase("NoSymbols1234")]
    public async Task CreateUser_ShouldRejectPassword_WhenItDoesNotMeetThePolicy(string password)
    {
        // Act
        var result = await _userManager.CreateAsync(
            new AppUser { UserName = "another-user", CreatedAt = DateTime.UtcNow },
            password);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [TestCase(nameof(AuthController.Login))]
    [TestCase(nameof(AuthController.SetupAdmin))]
    public void Endpoint_ShouldBeRateLimited(string methodName)
    {
        // Act
        var attribute = typeof(AuthController).GetMethod(methodName)!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>()
            .Single();

        // Assert
        attribute.PolicyName.Should().Be(AuthProtection.RateLimitPolicy);
    }

    private Task<ActionResult<AuthResponse>> LoginAsync(string password)
    {
        return _controller.Login(new LoginRequest { Identifier = UserName, Password = password });
    }

    private async Task LockAccountAsync()
    {
        for (var attempt = 0; attempt < AuthProtection.MaxFailedAccessAttempts; attempt++)
        {
            await LoginAsync(WrongPassword);
        }
    }
}
