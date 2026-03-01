using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;

namespace OpenShort.Tests.Api;

[TestFixture]
public class RedirectControllerTests
{
    private Mock<ILogger<RedirectController>> _mockLogger;
    private RedirectController _controller;
    private Mock<ILinkService> _mockLinkService;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<RedirectController>>();
        
        _mockLinkService = new Mock<ILinkService>();
        
        _controller = new RedirectController(_mockLogger.Object, _mockLinkService.Object);

        // Mock HttpContext for Host request
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("localhost");
        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = httpContext
        };
    }

    [Test]
    public async Task RedirectToUrl_ShouldRedirect_WhenLinkExistsAndActive()
    {
        // Arrange
        var link = new Link
        {
            Slug = "go",
            DestinationUrl = "https://google.com",
            Domain = "localhost",
            IsActive = true,
            RedirectType = RedirectType.Permanent
        };
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "go")).ReturnsAsync(link);

        // Act
        var result = await _controller.RedirectToUrl("go");

        // Assert
        result.Should().BeOfType<RedirectResult>();
        var redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("https://google.com");
        redirectResult.Permanent.Should().BeTrue();
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFound_WhenLinkDoesNotExist()
    {
        // Arrange
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "unknown")).ReturnsAsync((Link?)null);

        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFound_WhenLinkIsExpired()
    {
        // Arrange
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "expired")).ReturnsAsync((Link?)null); // Service returns null if expired

        // Act
        var result = await _controller.RedirectToUrl("expired");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
