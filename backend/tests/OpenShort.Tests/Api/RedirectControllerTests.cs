using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Tests.Api;

[TestFixture]
public class RedirectControllerTests
{
    private AppDbContext _context;
    private Mock<ILogger<RedirectController>> _mockLogger;
    private RedirectController _controller;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _mockLogger = new Mock<ILogger<RedirectController>>();
        _controller = new RedirectController(_context, _mockLogger.Object);

        // Mock HttpContext for Host request
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("localhost");
        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = httpContext
        };
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
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
        _context.Links.Add(link);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.RedirectToUrl("go");

        // Assert
        result.Should().BeOfType<RedirectResult>();
        var redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("https://google.com");
        redirectResult.Permanent.Should().BeTrue();

        // Verify Tracking
        var dbLink = await _context.Links.FindAsync(link.Id);
        dbLink!.ClickCount.Should().Be(1);
        dbLink.LastAccessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFound_WhenLinkDoesNotExist()
    {
        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFound_WhenLinkIsExpired()
    {
        // Arrange
        _context.Links.Add(new Link
        {
            Slug = "expired",
            DestinationUrl = "https://old.com",
            Domain = "localhost",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.RedirectToUrl("expired");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
