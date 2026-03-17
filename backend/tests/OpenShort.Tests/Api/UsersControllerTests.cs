using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
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
    private UsersController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _userServiceMock = new Mock<IUserService>();
        _loggerMock = new Mock<ILogger<UsersController>>();
        _controller = new UsersController(_userServiceMock.Object, _loggerMock.Object);
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

        _userServiceMock.Verify(service => service.DeleteAdminAsync(It.IsAny<string>()), Times.Never);
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
            .Setup(service => service.DeleteAdminAsync("user-456"))
            .ReturnsAsync((false, new[]
            {
                new IdentityError
                {
                    Code = "LastAdminDeletionForbidden",
                    Description = "You cannot delete the last remaining admin user."
                }
            }));

        var result = await _controller.DeleteUser("user-456");

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
