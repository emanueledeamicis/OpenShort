using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using OpenShort.Api.Auth;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Api;

[TestFixture]
public class UsersControllerTests
{
    private Mock<IUserService> _userServiceMock = null!;
    private Mock<ILogger<UsersController>> _loggerMock = null!;
    private TwoFactorCodeThrottle _twoFactorCodeThrottle = null!;
    private UsersController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _userServiceMock = new Mock<IUserService>();
        _loggerMock = new Mock<ILogger<UsersController>>();
        _twoFactorCodeThrottle = new TwoFactorCodeThrottle(TimeProvider.System, NullLogger<LoginThrottle>.Instance);
        _controller = new UsersController(_userServiceMock.Object, _twoFactorCodeThrottle, _loggerMock.Object);
    }

    [Test]
    public void UsersController_ShouldRequireAdminJwtAuthentication()
    {
        var attribute = typeof(UsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attribute.AuthenticationSchemes.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        attribute.Roles.Should().Be(DatabaseInitializer.AdminRoleName);
    }

    [Test]
    public async Task DeleteUser_ShouldRejectDeletingCurrentUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-123")],
                    authenticationType: "TestAuth"))
            }
        };

        var result = await _controller.DeleteUser("user-123");

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _userServiceMock.Verify(service => service.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task GetUsers_ShouldTellWhoHasTwoFactorOn()
    {
        // Arrange
        _userServiceMock
            .Setup(service => service.GetAllAsync())
            .ReturnsAsync(
            [
                new UserAccount(new AppUser { Id = "user-1", UserName = "admin", TwoFactorEnabled = true }, IsAdmin: true),
                new UserAccount(new AppUser { Id = "user-2", UserName = "second@example.com" }, IsAdmin: false)
            ]);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var users = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeAssignableTo<IEnumerable<UserDto>>().Subject.ToList();
        users.Single(user => user.Id == "user-1").TwoFactorEnabled.Should().BeTrue();
        users.Single(user => user.Id == "user-2").TwoFactorEnabled.Should().BeFalse();
        users.Single(user => user.Id == "user-1").IsAdmin.Should().BeTrue();
        users.Single(user => user.Id == "user-2").IsAdmin.Should().BeFalse();
    }

    [Test]
    public async Task DisableTwoFactor_ShouldReturnNoContent_ForAnotherUser()
    {
        // Arrange
        SignInAs("user-123");
        _userServiceMock
            .Setup(service => service.DisableTwoFactorAsync("user-456"))
            .ReturnsAsync((true, Enumerable.Empty<IdentityError>()));

        // Act
        var result = await _controller.DisableTwoFactor("user-456");

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task DisableTwoFactor_ShouldRejectTheCurrentUser()
    {
        // Arrange
        SignInAs("user-123");

        // Act
        var result = await _controller.DisableTwoFactor("user-123");

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _userServiceMock.Verify(service => service.DisableTwoFactorAsync(It.IsAny<string>()), Times.Never);
    }

    // MySQL finds an account from an id written in another case
    [Test]
    public async Task DisableTwoFactor_ShouldRejectTheCurrentUser_WhenTheIdIsWrittenInAnotherCase()
    {
        // Arrange
        SignInAs("user-123");
        _userServiceMock
            .Setup(service => service.GetByIdAsync("USER-123"))
            .ReturnsAsync(new AppUser { Id = "user-123", UserName = "admin" });

        // Act
        var result = await _controller.DisableTwoFactor("USER-123");

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _userServiceMock.Verify(service => service.DisableTwoFactorAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task DeleteUser_ShouldRejectDeletingCurrentUser_WhenTheIdIsWrittenInAnotherCase()
    {
        // Arrange
        SignInAs("user-123");
        _userServiceMock
            .Setup(service => service.GetByIdAsync("USER-123"))
            .ReturnsAsync(new AppUser { Id = "user-123", UserName = "admin" });

        // Act
        var result = await _controller.DeleteUser("USER-123");

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _userServiceMock.Verify(service => service.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task DisableTwoFactor_ShouldForgetTheWrongCodesOfTheLogin()
    {
        // Arrange: enough wrong codes at login to have a delay pending
        SignInAs("user-123");
        for (var failures = 0; failures < 10; failures++)
        {
            await _twoFactorCodeThrottle.AttemptAsync("user-456", () => Task.FromResult(false));
        }

        _userServiceMock
            .Setup(service => service.DisableTwoFactorAsync("user-456"))
            .ReturnsAsync((true, Enumerable.Empty<IdentityError>()));

        // Act
        await _controller.DisableTwoFactor("user-456");

        // Assert: after turning it on again, the first code is checked
        var attempt = await _twoFactorCodeThrottle.AttemptAsync("user-456", () => Task.FromResult(true));
        attempt.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task DisableTwoFactor_ShouldReturnNoContent_WhenItIsOffButTheSecretWasNotRemoved()
    {
        // Arrange
        SignInAs("user-123");
        _userServiceMock
            .Setup(service => service.DisableTwoFactorAsync("user-456"))
            .ReturnsAsync((true, new[] { new IdentityError { Code = "ConcurrencyFailure", Description = "Not removed." } }));

        // Act
        var result = await _controller.DisableTwoFactor("user-456");

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [TestCase("NotFound", StatusCodes.Status404NotFound)]
    [TestCase("TwoFactorNotEnabled", StatusCodes.Status409Conflict)]
    [TestCase("ConcurrencyFailure", StatusCodes.Status500InternalServerError)]
    public async Task DisableTwoFactor_ShouldReturnTheStatusOfTheError(string errorCode, int expectedStatusCode)
    {
        // Arrange
        SignInAs("user-123");
        _userServiceMock
            .Setup(service => service.DisableTwoFactorAsync("user-456"))
            .ReturnsAsync((false, new[] { new IdentityError { Code = errorCode, Description = "Not done." } }));

        // Act
        var result = await _controller.DisableTwoFactor("user-456");

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(expectedStatusCode);
    }

    private void SignInAs(string userId)
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)],
                    authenticationType: "TestAuth"))
            }
        };
    }

    [Test]
    public async Task CreateUser_ShouldUseRequestBodyValues()
    {
        var createdUser = new AppUser
        {
            Id = "user-1",
            UserName = "new@example.com",
            Email = "new@example.com",
            CreatedAt = DateTime.UtcNow
        };

        _userServiceMock
            .Setup(service => service.CreateAdminAsync("new@example.com", "StrongPass123!"))
            .ReturnsAsync((createdUser, Enumerable.Empty<IdentityError>()));

        var result = await _controller.CreateUser(new CreateUserRequest
        {
            Email = "new@example.com",
            Password = "StrongPass123!"
        });

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Test]
    public async Task DeleteUser_ShouldReturnBadRequest_WhenDeletingLastAdmin()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-123")],
                    authenticationType: "TestAuth"))
            }
        };

        _userServiceMock
            .Setup(service => service.DeleteAsync("user-456"))
            .ReturnsAsync((false, new[]
            {
                new IdentityError
                {
                    Code = "LastAdminDeletionForbidden",
                    Description = "You cannot delete the last admin user who can sign in."
                }
            }));

        var result = await _controller.DeleteUser("user-456");

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
