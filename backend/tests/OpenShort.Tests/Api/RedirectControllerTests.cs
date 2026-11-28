using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
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
    private Mock<IDomainService> _mockDomainService;
    private Mock<IWebHostEnvironment> _mockEnvironment;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<RedirectController>>();
        
        _mockLinkService = new Mock<ILinkService>();
        
        _mockDomainService = new Mock<IDomainService>();

        // Point to a folder without the missing-link template so the built-in fallback page is used
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockEnvironment.Setup(e => e.WebRootPath).Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        _controller = new RedirectController(
            _mockLogger.Object,
            _mockLinkService.Object,
            _mockDomainService.Object,
            _mockEnvironment.Object);

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
    public async Task RedirectToUrl_ShouldReturnNotFoundPage_WhenLinkDoesNotExist()
    {
        // Arrange
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "unknown")).ReturnsAsync((Link?)null);

        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<ContentResult>();
        var contentResult = result as ContentResult;
        contentResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        contentResult.ContentType.Should().StartWith("text/html");
        contentResult.Content.Should().Contain("localhost/unknown");
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFoundPage_WhenLinkIsExpired()
    {
        // Arrange
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "expired")).ReturnsAsync((Link?)null); // Service returns null if expired

        // Act
        var result = await _controller.RedirectToUrl("expired");

        // Assert
        result.Should().BeOfType<ContentResult>();
        (result as ContentResult)!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task RedirectToUrl_ShouldHtmlEncodeSlug_InNotFoundPage()
    {
        // Arrange
        var slug = "<script>alert(1)</script>";
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", slug)).ReturnsAsync((Link?)null);

        // Act
        var result = await _controller.RedirectToUrl(slug);

        // Assert
        result.Should().BeOfType<ContentResult>();
        var contentResult = result as ContentResult;
        contentResult!.Content.Should().NotContain("<script>");
        contentResult.Content.Should().Contain("&lt;script&gt;");
    }

    // The hosts are stored in lower case: the link and the settings of the domain are looked up in that form
    [Test]
    public async Task RedirectToUrl_ShouldLookTheHostUpInLowerCase_WhenTheRequestWritesItInUpperCase()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Request.Host = new HostString("Short.Example.COM", 8080);
        var domain = new Domain
        {
            Host = "short.example.com",
            NotFoundBehavior = DomainNotFoundBehavior.RedirectToCustomUrl,
            NotFoundRedirectUrl = "https://example.com/not-found"
        };
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("short.example.com", "unknown")).ReturnsAsync((Link?)null);
        _mockDomainService.Setup(s => s.GetByHostAsync("short.example.com")).ReturnsAsync(domain);

        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be("https://example.com/not-found");
        _mockLinkService.Verify(s => s.ResolveAndTrackRedirectAsync("short.example.com", "unknown"), Times.Once);
    }

    [Test]
    public async Task RedirectToUrl_ShouldRedirectToCustomUrl_WhenDomainHasCustomNotFoundUrl()
    {
        // Arrange
        var domain = new Domain
        {
            Host = "localhost",
            NotFoundBehavior = DomainNotFoundBehavior.RedirectToCustomUrl,
            NotFoundRedirectUrl = "https://example.com/not-found"
        };
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "unknown")).ReturnsAsync((Link?)null);
        _mockDomainService.Setup(s => s.GetByHostAsync("localhost")).ReturnsAsync(domain);

        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<RedirectResult>();
        var redirectResult = result as RedirectResult;
        redirectResult!.Url.Should().Be("https://example.com/not-found");
        redirectResult.Permanent.Should().BeFalse();
    }

    // A value stored by an earlier version, which accepted every scheme, is not followed
    [TestCase("not-a-url")]
    [TestCase("/path")]
    [TestCase("javascript:alert(1)")]
    [TestCase("file:///etc/passwd")]
    [TestCase("ftp://example.com/file")]
    public async Task RedirectToUrl_ShouldReturnNotFoundPage_WhenCustomNotFoundUrlIsInvalid(string storedUrl)
    {
        // Arrange
        var domain = new Domain
        {
            Host = "localhost",
            NotFoundBehavior = DomainNotFoundBehavior.RedirectToCustomUrl,
            NotFoundRedirectUrl = storedUrl
        };
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "unknown")).ReturnsAsync((Link?)null);
        _mockDomainService.Setup(s => s.GetByHostAsync("localhost")).ReturnsAsync(domain);

        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<ContentResult>();
        (result as ContentResult)!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFoundPage_WhenHostIsNotRegistered()
    {
        // Arrange
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "unknown")).ReturnsAsync((Link?)null);
        _mockDomainService.Setup(s => s.GetByHostAsync("localhost")).ReturnsAsync((Domain?)null);

        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<ContentResult>();
        (result as ContentResult)!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        _mockDomainService.Verify(s => s.GetByHostAsync("localhost"), Times.Once);
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnNotFoundPage_WhenBehaviorIsOpenShortPageEvenIfCustomUrlIsSet()
    {
        // Arrange
        var domain = new Domain
        {
            Host = "localhost",
            NotFoundBehavior = DomainNotFoundBehavior.OpenShortPage,
            NotFoundRedirectUrl = "https://example.com/not-found"
        };
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "unknown")).ReturnsAsync((Link?)null);
        _mockDomainService.Setup(s => s.GetByHostAsync("localhost")).ReturnsAsync(domain);

        // Act
        var result = await _controller.RedirectToUrl("unknown");

        // Assert
        result.Should().BeOfType<ContentResult>();
        (result as ContentResult)!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task RedirectToUrl_ShouldUseTemplateFile_AndHtmlEncodeHostAndSlug()
    {
        // Arrange
        var webRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(webRoot);
        try
        {
            File.WriteAllText(Path.Combine(webRoot, "missing-link.html"), "<p id=\"custom-template\">{{HOST}}|{{SLUG}}</p>");
            _mockEnvironment.Setup(e => e.WebRootPath).Returns(webRoot);
            _controller.ControllerContext.HttpContext.Request.Host = new HostString("<b>host");

            var slug = "<script>alert(1)</script>";
            _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("<b>host", slug)).ReturnsAsync((Link?)null);

            // Act
            var result = await _controller.RedirectToUrl(slug);

            // Assert
            result.Should().BeOfType<ContentResult>();
            var contentResult = result as ContentResult;
            contentResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            contentResult.Content.Should().Be("<p id=\"custom-template\">&lt;b&gt;host|&lt;script&gt;alert(1)&lt;/script&gt;</p>");
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Test]
    public async Task RedirectToUrl_ShouldUseContentRootWwwroot_WhenWebRootPathIsNull()
    {
        // Arrange
        var contentRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(contentRoot, "wwwroot"));
        try
        {
            File.WriteAllText(Path.Combine(contentRoot, "wwwroot", "missing-link.html"), "<p id=\"content-root-template\">{{SLUG}}</p>");
            _mockEnvironment.Setup(e => e.WebRootPath).Returns((string)null!);
            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(contentRoot);
            _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "unknown")).ReturnsAsync((Link?)null);

            // Act
            var result = await _controller.RedirectToUrl("unknown");

            // Assert
            result.Should().BeOfType<ContentResult>();
            (result as ContentResult)!.Content.Should().Be("<p id=\"content-root-template\">unknown</p>");
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task RedirectToUrl_ShouldReturnBadRequest_WhenSlugIsEmpty(string slug)
    {
        // Act
        var result = await _controller.RedirectToUrl(slug);

        // Assert
        result.Should().BeOfType<BadRequestResult>();
        _mockLinkService.Verify(s => s.ResolveAndTrackRedirectAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task RedirectToUrl_ShouldReturnServerError_WhenServiceThrows()
    {
        // Arrange
        _mockLinkService.Setup(s => s.ResolveAndTrackRedirectAsync("localhost", "boom")).ThrowsAsync(new InvalidOperationException("db down"));

        // Act
        var result = await _controller.RedirectToUrl("boom");

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problemDetails = (result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("An error occurred while processing the redirect.");
        problemDetails.Detail.Should().NotContain("db down");
    }
}
