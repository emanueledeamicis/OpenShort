using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NUnit.Framework;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;
using OpenShort.Infrastructure.Services;
using System.Threading.Channels;

namespace OpenShort.Tests.Api;

[TestFixture]
public class LinksControllerTests
{
    private AppDbContext _context;
    private Mock<ISlugGenerator> _mockSlugGenerator;
    private Mock<ILogger<LinksController>> _mockLogger;
    private LinksController _controller;
    private DomainService _domainService;
    private LinkService _linkService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _mockSlugGenerator = new Mock<ISlugGenerator>();
        _mockLogger = new Mock<ILogger<LinksController>>();
        var redirectCache = new RedirectCache(new MemoryCache(new MemoryCacheOptions()));
        _domainService = new DomainService(_context, redirectCache);

        var mockSettingService = new Mock<ISettingService>();
        var channel = Channel.CreateUnbounded<ClickEvent>();
        var mockLinkLogger = new Mock<ILogger<LinkService>>();
        _linkService = new LinkService(_context, _mockSlugGenerator.Object, redirectCache, mockSettingService.Object, channel.Writer, mockLinkLogger.Object);

        // Seed default domain for existing tests
        _context.Domains.Add(new Domain { Host = "localhost", IsActive = true });
        _context.Domains.Add(new Domain { Host = "inactive.com", IsActive = false });
        _context.SaveChanges();

        _controller = new LinksController(_linkService, _domainService, _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task CreateLink_ShouldCreateLink_WhenValid()
    {
        // Arrange
        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://example.com",
            Slug = "test-slug",
            Domain = "localhost"
        };

        // Act
        var result = await _controller.CreateLink(dto);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        var link = createdResult!.Value as Link;
        
        link.Should().NotBeNull();
        link!.Slug.Should().Be("test-slug");
        link.DestinationUrl.Should().Be("https://example.com/");

        // Verify DB
        var dbLink = await _context.Links.FirstOrDefaultAsync(l => l.Slug == "test-slug");
        dbLink.Should().NotBeNull();
    }

    [Test]
    public async Task CreateLink_ShouldAutoGenerateSlug_WhenSlugIsEmpty()
    {
        // Arrange
        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://example.com",
            Slug = null,
            Domain = "localhost"
        };

        _mockSlugGenerator.Setup(x => x.GenerateSlug()).Returns("auto-slug");

        // Act
        var result = await _controller.CreateLink(dto);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        var link = createdResult!.Value as Link;
        
        link!.Slug.Should().Be("auto-slug");
    }

    [Test]
    public async Task CreateLink_ShouldReturnConflict_WhenSlugExists()
    {
        // Arrange
        _context.Links.Add(new Link
        {
            Slug = "existing",
            DestinationUrl = "https://old.com",
            Domain = "localhost" // Must exist
        });
        await _context.SaveChangesAsync();

        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://new.com",
            Slug = "existing",
            Domain = "localhost"
        };

        // Act
        var result = await _controller.CreateLink(dto);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
            
        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("Slug already in use for this domain.");
    }

    [TestCase("invalid-url")]
    [TestCase("/path")]
    [TestCase("javascript:alert(1)")]
    [TestCase("data:text/html,<p>x</p>")]
    [TestCase("file:///etc/passwd")]
    [TestCase("ftp://example.com/file")]
    [TestCase("mailto:someone@example.com")]
    public async Task CreateLink_ShouldReturnBadRequest_WhenUrlIsNotAnHttpOrHttpsUrl(string destinationUrl)
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = destinationUrl,
            Domain = "localhost"
        };

        var result = await _controller.CreateLink(dto);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("Destination URL must be an absolute http or https URL of at most 2048 characters.");
        _context.Links.Should().BeEmpty();
    }

    // The stored form adds the slash after the host: it must fit the column as well
    [Test]
    public async Task CreateLink_ShouldReturnBadRequest_WhenTheStoredUrlIsLongerThanTheColumn()
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://example.com?" + new string('a', 2028),
            Slug = "long",
            Domain = "localhost"
        };
        dto.DestinationUrl.Length.Should().Be(2048);

        var result = await _controller.CreateLink(dto);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _context.Links.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateLink_ShouldReturnBadRequest_WhenTheNewUrlIsNotAnHttpOrHttpsUrl()
    {
        var link = await AddLinkAsync("https://example.com/");

        var result = await _controller.UpdateLink(link.Id, new UpdateLinkDto { DestinationUrl = "ftp://example.com/file" });

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _context.ChangeTracker.Clear();
        (await _context.Links.FindAsync(link.Id))!.DestinationUrl.Should().Be("https://example.com/");
    }

    [Test]
    public async Task UpdateLink_ShouldStoreTheNewUrl_WhenItIsAnHttpOrHttpsUrl()
    {
        var link = await AddLinkAsync("https://example.com/");

        var result = await _controller.UpdateLink(link.Id, new UpdateLinkDto { DestinationUrl = "HTTPS://Example.org/New" });

        result.Should().BeOfType<NoContentResult>();
        _context.ChangeTracker.Clear();
        (await _context.Links.FindAsync(link.Id))!.DestinationUrl.Should().Be("https://example.org/New");
    }

    // A link created by an earlier version with another scheme can still be renamed or turned off
    [Test]
    public async Task UpdateLink_ShouldKeepADestinationWithAnotherScheme_WhenItIsNotChanged()
    {
        var link = await AddLinkAsync("ftp://example.com/file");

        var result = await _controller.UpdateLink(link.Id, new UpdateLinkDto
        {
            DestinationUrl = "ftp://example.com/file",
            Title = "Renamed",
            IsActive = false
        });

        result.Should().BeOfType<NoContentResult>();
        _context.ChangeTracker.Clear();
        var stored = await _context.Links.FindAsync(link.Id);
        stored!.DestinationUrl.Should().Be("ftp://example.com/file");
        stored.Title.Should().Be("Renamed");
        stored.IsActive.Should().BeFalse();
    }

    private async Task<Link> AddLinkAsync(string destinationUrl)
    {
        var link = new Link { Slug = "stored", Domain = "localhost", DestinationUrl = destinationUrl };
        _context.Links.Add(link);
        await _context.SaveChangesAsync();
        return link;
    }

    [Test]
    public async Task CreateLink_ShouldReturnBadRequest_WhenDomainIsMissing()
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://valid.com",
            Domain = "" 
        };

        var result = await _controller.CreateLink(dto);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            
        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("Domain is required.");
    }

    [Test]
    public async Task CreateLink_ShouldReturnBadRequest_WhenDomainIsNotAuthorized()
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://valid.com",
            Domain = "hacker.com"
        };

        var result = await _controller.CreateLink(dto);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            
        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("Domain 'hacker.com' is not authorized.");
    }

    [Test]
    public async Task CreateLink_ShouldReturnBadRequest_WhenDomainIsNotActive()
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://valid.com",
            Domain = "inactive.com"
        };

        var result = await _controller.CreateLink(dto);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            
        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("Domain 'inactive.com' is not active.");
    }
}
