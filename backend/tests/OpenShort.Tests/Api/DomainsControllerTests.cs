using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Infrastructure.Data;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Api;

[TestFixture]
public class DomainsControllerTests
{
    private AppDbContext _context = null!;
    private Mock<ILogger<DomainsController>> _loggerMock = null!;
    private DomainsController _controller = null!;
    private DomainService _domainService = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<DomainsController>>();
        _domainService = new DomainService(_context, new RedirectCache(new MemoryCache(new MemoryCacheOptions())));
        _controller = new DomainsController(_domainService, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task GetDomains_ShouldReturnAllDomains()
    {
        // Arrange
        _context.Domains.Add(new Domain { Host = "example.com", IsActive = true });
        _context.Domains.Add(new Domain { Host = "test.com", IsActive = false });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDomains();

        // Assert
        result.Value.Should().HaveCount(2);
    }

    [Test]
    public async Task GetDomain_ShouldReturnDomain_WhenExists()
    {
        // Arrange
        var domain = new Domain { Host = "example.com", IsActive = true };
        _context.Domains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDomain(domain.Id);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Host.Should().Be("example.com");
    }

    [Test]
    public async Task GetDomain_ShouldReturnNotFound_WhenDoesNotExist()
    {
        // Act
        var result = await _controller.GetDomain(999);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            
        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("Domain not found.");
    }

    [Test]
    public async Task CreateDomain_ShouldCreateDomain_WhenValid()
    {
        // Arrange
        var dto = new CreateDomainDto { Host = "new.com" };

        // Act
        var result = await _controller.CreateDomain(dto);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        var createdDomain = createdResult!.Value as Domain;

        createdDomain.Should().NotBeNull();
        createdDomain!.Host.Should().Be("new.com");
        createdDomain.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task CreateDomain_ShouldReturnConflict_WhenDomainExists()
    {
        // Arrange
        _context.Domains.Add(new Domain { Host = "existing.com", IsActive = true });
        await _context.SaveChangesAsync();

        var dto = new CreateDomainDto { Host = "existing.com" };

        // Act
        var result = await _controller.CreateDomain(dto);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
            
        var problemDetails = (result.Result as ObjectResult)!.Value as ProblemDetails;
        problemDetails!.Detail.Should().Be("Domain already exists.");
    }

    [Test]
    public async Task UpdateDomain_ShouldReturnNotFound_WhenDoesNotExist()
    {
        // Arrange
        var dto = new UpdateDomainDto { IsActive = true, NotFoundBehavior = DomainNotFoundBehavior.OpenShortPage };

        // Act
        var result = await _controller.UpdateDomain(999, dto);

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task UpdateDomain_ShouldSaveCustomNotFoundUrl_WhenValid()
    {
        // Arrange
        var domain = new Domain { Host = "example.com", IsActive = true };
        _context.Domains.Add(domain);
        await _context.SaveChangesAsync();

        var dto = new UpdateDomainDto
        {
            IsActive = false,
            NotFoundBehavior = DomainNotFoundBehavior.RedirectToCustomUrl,
            NotFoundRedirectUrl = "  https://example.com/not-found  "
        };

        // Act
        var result = await _controller.UpdateDomain(domain.Id, dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Drop tracked entities so the assertions read what was actually persisted
        _context.ChangeTracker.Clear();
        var dbDomain = await _context.Domains.FindAsync(domain.Id);
        dbDomain!.IsActive.Should().BeFalse();
        dbDomain.NotFoundBehavior.Should().Be(DomainNotFoundBehavior.RedirectToCustomUrl);
        dbDomain.NotFoundRedirectUrl.Should().Be("https://example.com/not-found");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-url")]
    public async Task UpdateDomain_ShouldReturnBadRequest_WhenCustomNotFoundUrlIsInvalid(string? url)
    {
        // Arrange
        var domain = new Domain { Host = "example.com", IsActive = true };
        _context.Domains.Add(domain);
        await _context.SaveChangesAsync();

        var dto = new UpdateDomainDto
        {
            IsActive = true,
            NotFoundBehavior = DomainNotFoundBehavior.RedirectToCustomUrl,
            NotFoundRedirectUrl = url
        };

        // Act
        var result = await _controller.UpdateDomain(domain.Id, dto);

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Drop tracked entities so the assertions read what was actually persisted
        _context.ChangeTracker.Clear();
        var dbDomain = await _context.Domains.FindAsync(domain.Id);
        dbDomain!.NotFoundBehavior.Should().Be(DomainNotFoundBehavior.OpenShortPage);
        dbDomain.NotFoundRedirectUrl.Should().BeNull();
    }

    [Test]
    public async Task UpdateDomain_ShouldClearCustomNotFoundUrl_WhenBehaviorIsOpenShortPage()
    {
        // Arrange
        var domain = new Domain
        {
            Host = "example.com",
            IsActive = true,
            NotFoundBehavior = DomainNotFoundBehavior.RedirectToCustomUrl,
            NotFoundRedirectUrl = "https://example.com/not-found"
        };
        _context.Domains.Add(domain);
        await _context.SaveChangesAsync();

        var dto = new UpdateDomainDto
        {
            IsActive = true,
            NotFoundBehavior = DomainNotFoundBehavior.OpenShortPage,
            NotFoundRedirectUrl = "https://example.com/ignored"
        };

        // Act
        var result = await _controller.UpdateDomain(domain.Id, dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Drop tracked entities so the assertions read what was actually persisted
        _context.ChangeTracker.Clear();
        var dbDomain = await _context.Domains.FindAsync(domain.Id);
        dbDomain!.NotFoundBehavior.Should().Be(DomainNotFoundBehavior.OpenShortPage);
        dbDomain.NotFoundRedirectUrl.Should().BeNull();
    }

    [Test]
    public async Task DeleteDomain_ShouldDelete_WhenExists()
    {
        // Arrange
        var domain = new Domain { Host = "delete.com", IsActive = true };
        _context.Domains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteDomain(domain.Id);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        var dbDomain = await _context.Domains.FindAsync(domain.Id);
        dbDomain.Should().BeNull();
    }
}
