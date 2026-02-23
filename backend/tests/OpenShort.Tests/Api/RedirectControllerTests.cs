using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using System.Threading.Channels;

namespace OpenShort.Tests.Api;

[TestFixture]
public class RedirectControllerTests
{
    private Mock<ILogger<RedirectController>> _mockLogger;
    private RedirectController _controller;
    private Mock<ILinkService> _mockLinkService;
    private Channel<ClickEvent> _channel;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<RedirectController>>();
        
        _mockLinkService = new Mock<ILinkService>();
        _channel = Channel.CreateUnbounded<ClickEvent>();
        
        _controller = new RedirectController(_mockLogger.Object, _mockLinkService.Object, _channel.Writer);

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
        _mockLinkService.Setup(s => s.GetCachedLinkAsync("localhost", "go")).ReturnsAsync(link);

        // Act
        var result = await _controller.RedirectToUrl("go");

        // Assert
        result.Should().BeOfType<RedirectResult>();
        var redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("https://google.com");
        redirectResult.Permanent.Should().BeTrue();

        // Verify Tracking (Event queued)
        _channel.Reader.Count.Should().Be(1);
        var clickEvent = await _channel.Reader.ReadAsync();
        clickEvent.Slug.Should().Be(link.Slug);
        clickEvent.Domain.Should().Be(link.Domain);
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFound_WhenLinkDoesNotExist()
    {
        // Arrange
        _mockLinkService.Setup(s => s.GetCachedLinkAsync("localhost", "unknown")).ReturnsAsync((Link?)null);

        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFound_WhenLinkIsExpired()
    {
        // Arrange
        _mockLinkService.Setup(s => s.GetCachedLinkAsync("localhost", "expired")).ReturnsAsync((Link?)null); // Service returns null if expired

        // Act
        var result = await _controller.RedirectToUrl("expired");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
