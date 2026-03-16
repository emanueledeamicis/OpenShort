using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using OpenShort.Api.Controllers;
using OpenShort.Api.Models;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Api;

[TestFixture]
public class AuthControllerTests
{
    private Mock<UserManager<IdentityUser>> _userManagerMock = null!;
    private Mock<ITokenService> _tokenServiceMock = null!;
    private Mock<ISettingService> _settingServiceMock = null!;
    private Mock<ILogger<AuthController>> _loggerMock = null!;
    private AuthController _controller = null!;

    [SetUp]
    public void Setup()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        _userManagerMock = new Mock<UserManager<IdentityUser>>(
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
            _settingServiceMock.Object,
            _loggerMock.Object);
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
        var adminUser = new IdentityUser
        {
            UserName = "admin"
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

    [Test]
    public void Register_ShouldRequireAdminJwtAuthentication()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Register));

        var attribute = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attribute.AuthenticationSchemes.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        attribute.Roles.Should().Be(DatabaseInitializer.AdminRoleName);
    }
}
