using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;

using OpenShort.Infrastructure.Services;
using OpenShort.Core.Interfaces;

namespace OpenShort.Tests.Api;

[TestFixture]
public class LinksControllerTests
{
    private AppDbContext _context;
    private Mock<ISlugGenerator> _mockSlugGenerator;
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
        _domainService = new DomainService(_context);
        _linkService = new LinkService(_context, _mockSlugGenerator.Object);

        // Seed default domain for existing tests
        _context.Domains.Add(new Domain { Host = "localhost", IsActive = true });
        _context.Domains.Add(new Domain { Host = "inactive.com", IsActive = false });
        _context.SaveChanges();

        _controller = new LinksController(_linkService, _domainService);
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
        link.DestinationUrl.Should().Be("https://example.com");

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

    [Test]
    public async Task CreateLink_ShouldReturnBadRequest_WhenUrlIsInvalid()
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = "invalid-url",
            Domain = "localhost"
        };

        var result = await _controller.CreateLink(dto);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("Invalid Destination URL format.");
    }

    [Test]
    public async Task CreateLink_ShouldReturnBadRequest_WhenUrlSchemIsDangerous()
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = "javascript:alert(1)",
            Domain = "localhost"
        };

        var result = await _controller.CreateLink(dto);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            
        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("URL scheme is not allowed.");
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
