using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private const string Email = "owner@example.com";
    private const string Password = "StrongPass123!";
    private const string WrongPassword = "WrongPass123!";
    private const string AttackerAddress = "203.0.113.10";
    private const string OwnerAddress = "198.51.100.20";

    private ServiceProvider _serviceProvider = null!;
    private UserManager<AppUser> _userManager = null!;
    private Mock<ITokenService> _tokenServiceMock = null!;
    private Mock<ISettingService> _settingServiceMock = null!;
    private ManualTimeProvider _timeProvider = null!;
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
            new AppUser { UserName = UserName, Email = Email, CreatedAt = DateTime.UtcNow },
            Password);
        createResult.Succeeded.Should().BeTrue();

        _tokenServiceMock = new Mock<ITokenService>();
        _tokenServiceMock
            .Setup(service => service.CreateTokenAsync(It.IsAny<AppUser>(), It.IsAny<IList<string>>()))
            .ReturnsAsync("token-value");

        _settingServiceMock = new Mock<ISettingService>();
        _timeProvider = new ManualTimeProvider();
        _controller = new AuthController(
            _userManager,
            _tokenServiceMock.Object,
            new LoginThrottle(_timeProvider, NullLogger<LoginThrottle>.Instance),
            _settingServiceMock.Object,
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
        var result = await LoginAsync(Password, identifier: "nobody");

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Test]
    public async Task Login_ShouldReturnTooManyRequests_AfterRepeatedFailures()
    {
        // Arrange
        await FailLoginAsync(times: 3);

        // Act
        var result = await LoginAsync(WrongPassword);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        _controller.Response.Headers.RetryAfter.ToString().Should().Be("1");
    }

    [Test]
    public async Task Login_ShouldRejectCorrectPassword_WhileTheDelayIsPending()
    {
        // Arrange
        await FailLoginAsync(times: 3);

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
    public async Task Login_ShouldSucceed_WhenTheDelayHasElapsed()
    {
        // Arrange
        await FailLoginAsync(times: 3);
        _timeProvider.Advance(LoginThrottle.GetDelay(3));

        // Act
        var result = await LoginAsync(Password);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task Login_ShouldSucceedFromAnotherAddress_WhileAnAttackerIsDelayed()
    {
        // Arrange
        await FailLoginAsync(times: 20);

        // Act
        var attackerResult = await LoginAsync(Password);
        var ownerResult = await LoginAsync(Password, address: OwnerAddress);

        // Assert
        attackerResult.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        ownerResult.Result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task Login_ShouldDelayUnknownUsers_LikeExistingOnes()
    {
        // Arrange
        await FailLoginAsync(times: 3, identifier: "nobody");

        // Act
        var result = await LoginAsync(WrongPassword, identifier: "nobody");

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Test]
    public async Task Login_ShouldShareTheDelay_BetweenUserNameAndEmail()
    {
        // Arrange
        await FailLoginAsync(times: 3);

        // Act
        var result = await LoginAsync(Password, identifier: Email);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Test]
    public async Task Login_ShouldShareTheDelay_AmongSpellingsOfTheSameAccount()
    {
        // Arrange
        const string kelvinSign = "\u212A";
        var createResult = await _userManager.CreateAsync(
            new AppUser { UserName = "mark", CreatedAt = DateTime.UtcNow },
            Password);
        createResult.Succeeded.Should().BeTrue();
        await FailLoginAsync(times: 3, identifier: "mark");

        // Act
        var result = await LoginAsync(Password, identifier: "mar" + kelvinSign);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Test]
    public async Task Login_ShouldReturnClientClosedRequest_WhenTheRequestIsAborted()
    {
        // Arrange
        SetClientAddress(AttackerAddress);
        _controller.HttpContext.RequestAborted = new CancellationToken(canceled: true);

        // Act
        var result = await _controller.Login(new LoginRequest { Identifier = UserName, Password = Password });

        // Assert
        result.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
    }

    [Test]
    public async Task SetupAdmin_ShouldRemoveThePendingDelay_ForTheClientCompletingIt()
    {
        // Arrange
        var user = await _userManager.FindByNameAsync(UserName);
        (await _userManager.RemovePasswordAsync(user!)).Succeeded.Should().BeTrue();
        _settingServiceMock
            .Setup(service => service.GetSettingAsync(It.IsAny<string>(), true))
            .ReturnsAsync(true);
        await FailLoginAsync(times: 5);

        // Act
        var setupResult = await _controller.SetupAdmin(new SetupAdminRequest
        {
            Password = Password,
            ConfirmPassword = Password
        });
        var loginResult = await LoginAsync(Password);

        // Assert
        setupResult.Result.Should().BeOfType<OkObjectResult>();
        loginResult.Result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task SetupAdmin_ShouldReturnConflict_WhenAnotherAdministratorCanSignIn()
    {
        // Arrange: the setting says the setup is required, but an administrator with a password exists
        var admin = await _userManager.FindByNameAsync(UserName);
        (await _userManager.RemovePasswordAsync(admin!)).Succeeded.Should().BeTrue();
        await CreateAdministratorAsync("second@example.com", Password);
        _settingServiceMock
            .Setup(service => service.GetSettingAsync(It.IsAny<string>(), true))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.SetupAdmin(new SetupAdminRequest
        {
            Password = WrongPassword,
            ConfirmPassword = WrongPassword
        });

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
        (await _userManager.HasPasswordAsync(admin!)).Should().BeFalse();
        _tokenServiceMock.Verify(
            service => service.CreateTokenAsync(It.IsAny<AppUser>(), It.IsAny<IList<string>>()),
            Times.Never);
    }

    [Test]
    public async Task SetupAdmin_ShouldSucceed_WhenTheOnlyOtherAdministratorHasNoPassword()
    {
        // Arrange
        var admin = await _userManager.FindByNameAsync(UserName);
        (await _userManager.RemovePasswordAsync(admin!)).Succeeded.Should().BeTrue();
        await CreateAdministratorAsync("second@example.com", password: null);
        _settingServiceMock
            .Setup(service => service.GetSettingAsync(It.IsAny<string>(), true))
            .ReturnsAsync(true);
        SetClientAddress(OwnerAddress);

        // Act
        var result = await _controller.SetupAdmin(new SetupAdminRequest
        {
            Password = Password,
            ConfirmPassword = Password
        });

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        (await _userManager.CheckPasswordAsync(admin!, Password)).Should().BeTrue();
    }

    [Test]
    public async Task Login_ShouldNotLockTheAccountInTheDatabase_AfterRepeatedFailures()
    {
        // Act
        await FailLoginAsync(times: 20);

        // Assert
        var user = await _userManager.FindByNameAsync(UserName);
        (await _userManager.IsLockedOutAsync(user!)).Should().BeFalse();
        (await _userManager.GetAccessFailedCountAsync(user!)).Should().Be(0);
    }

    [Test]
    public void ConfigureIdentity_ShouldSetPasswordPolicy()
    {
        // Act
        var options = new IdentityOptions();
        AuthProtection.ConfigureIdentity(options);

        // Assert
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

    [TestCase(typeof(AuthController), nameof(AuthController.Login))]
    [TestCase(typeof(AuthController), nameof(AuthController.SetupAdmin))]
    [TestCase(typeof(SecurityController), nameof(SecurityController.ChangePassword))]
    public void Endpoint_ShouldBeRateLimited(Type controllerType, string methodName)
    {
        // Act
        var attribute = controllerType.GetMethod(methodName)!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>()
            .Single();

        // Assert
        attribute.PolicyName.Should().Be(AuthProtection.RateLimitPolicy);
    }

    private async Task CreateAdministratorAsync(string email, string? password)
    {
        var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            (await roleManager.CreateAsync(new IdentityRole("Admin"))).Succeeded.Should().BeTrue();
        }

        var user = new AppUser { UserName = email, Email = email, CreatedAt = DateTime.UtcNow };
        var createResult = password == null
            ? await _userManager.CreateAsync(user)
            : await _userManager.CreateAsync(user, password);
        createResult.Succeeded.Should().BeTrue();
        (await _userManager.AddToRoleAsync(user, "Admin")).Succeeded.Should().BeTrue();
    }

    private Task<ActionResult<AuthResponse>> LoginAsync(
        string password,
        string identifier = UserName,
        string address = AttackerAddress)
    {
        SetClientAddress(address);

        return _controller.Login(new LoginRequest { Identifier = identifier, Password = password });
    }

    private void SetClientAddress(string address)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private async Task FailLoginAsync(int times, string identifier = UserName)
    {
        for (var failures = 1; failures <= times; failures++)
        {
            var result = await LoginAsync(WrongPassword, identifier);
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();

            // Wait out the delay between attempts, so that each one is verified and counted.
            // The delay caused by the last failure is left pending.
            if (failures < times)
            {
                _timeProvider.Advance(LoginThrottle.GetDelay(failures));
            }
        }
    }
}
