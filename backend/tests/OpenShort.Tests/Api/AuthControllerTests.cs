using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenShort.Api.Auth;
using OpenShort.Api.Controllers;
using OpenShort.Api.Models;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Api;

[TestFixture]
public class AuthControllerTests
{
    private Mock<UserManager<AppUser>> _userManagerMock = null!;
    private Mock<ITokenService> _tokenServiceMock = null!;
    private Mock<ISettingService> _settingServiceMock = null!;
    private Mock<ILogger<AuthController>> _loggerMock = null!;
    private AuthController _controller = null!;

    [SetUp]
    public void Setup()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
        _tokenServiceMock = new Mock<ITokenService>();
        _settingServiceMock = new Mock<ISettingService>();
        _loggerMock = new Mock<ILogger<AuthController>>();

        _controller = new AuthController(
            _userManagerMock.Object,
            _tokenServiceMock.Object,
            new LoginThrottle(TimeProvider.System, NullLogger<LoginThrottle>.Instance),
            new TwoFactorCodeThrottle(TimeProvider.System, NullLogger<LoginThrottle>.Instance),
            new UsedTwoFactorCodes(TimeProvider.System),
            _settingServiceMock.Object,
            _loggerMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Test]
    public async Task GetSetupStatus_ShouldReturnConfiguredAdminUser()
    {
        _settingServiceMock
            .Setup(service => service.GetSettingAsync("InitialAdminSetupRequired", true))
            .ReturnsAsync(true);

        var result = await _controller.GetSetupStatus();

        result.Result.Should().BeOfType<OkObjectResult>();
        var payload = (result.Result as OkObjectResult)!.Value as InitialSetupStatusResponse;
        payload.Should().NotBeNull();
        payload!.IsSetupRequired.Should().BeTrue();
        payload.UserName.Should().Be("admin");
    }

    [Test]
    public async Task SetupAdmin_ShouldSetPassword_ClearFlag_AndReturnToken()
    {
        var adminUser = new AppUser
        {
            UserName = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _settingServiceMock
            .Setup(service => service.GetSettingAsync("InitialAdminSetupRequired", true))
            .ReturnsAsync(true);
        _userManagerMock
            .Setup(manager => manager.FindByNameAsync("admin"))
            .ReturnsAsync(adminUser);
        _userManagerMock
            .Setup(manager => manager.HasPasswordAsync(adminUser))
            .ReturnsAsync(false);
        _userManagerMock
            .Setup(manager => manager.GetUsersInRoleAsync("Admin"))
            .ReturnsAsync(new List<AppUser> { adminUser });
        _userManagerMock
            .Setup(manager => manager.AddPasswordAsync(adminUser, "StrongPass123!"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(manager => manager.GetRolesAsync(adminUser))
            .ReturnsAsync(Array.Empty<string>());
        _tokenServiceMock
            .Setup(service => service.CreateTokenAsync(adminUser, It.IsAny<IList<string>>()))
            .ReturnsAsync("token-value");

        var result = await _controller.SetupAdmin(new SetupAdminRequest
        {
            Password = "StrongPass123!",
            ConfirmPassword = "StrongPass123!"
        });

        result.Result.Should().BeOfType<OkObjectResult>();
        var payload = (result.Result as OkObjectResult)!.Value as AuthResponse;
        payload.Should().NotBeNull();
        payload!.UserName.Should().Be("admin");
        payload.Token.Should().Be("token-value");
        _settingServiceMock.Verify(
            service => service.SetSettingAsync(
                "InitialAdminSetupRequired",
                "false",
                It.IsAny<string>()),
            Times.Once);
    }

    // It created users without the Admin role, that the Users page did not show
    [Test]
    public void Register_ShouldNotExist()
    {
        typeof(AuthController).GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), inherit: true).Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>())
            .Select(attribute => attribute.Template)
            .Should().NotContain("register");
    }
}
