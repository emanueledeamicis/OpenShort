using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Infrastructure;

[TestFixture]
public class LinkServiceTests
{
    private AppDbContext _context = null!;
    private Mock<ISlugGenerator> _mockSlugGenerator = null!;
    private LinkService _linkService = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _mockSlugGenerator = new Mock<ISlugGenerator>();
        _linkService = new LinkService(_context, _mockSlugGenerator.Object);

        // Seed default domain
        _context.Domains.Add(new Domain { Host = "test.com", IsActive = true });
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task CreateAsync_ShouldGenerateSlugWhenEmpty()
    {
        // Arrange
        var generatedSlug = "abc123";
        _mockSlugGenerator.Setup(g => g.GenerateSlug()).Returns(generatedSlug);

        var newLink = new Link
        {
            Slug = "", // Empty slug triggers auto-generation
            Domain = "test.com",
            DestinationUrl = "https://example.com"
        };

        // Act
        var result = await _linkService.CreateAsync(newLink);

        // Assert
        result.Should().NotBeNull();
        result!.Slug.Should().Be(generatedSlug);
        _mockSlugGenerator.Verify(g => g.GenerateSlug(), Times.Once);
    }

    [Test]
    public async Task CreateAsync_ShouldKeepCustomSlugWhenProvided()
    {
        // Arrange
        var customSlug = "mycustom";

        var newLink = new Link
        {
            Slug = customSlug,
            Domain = "test.com",
            DestinationUrl = "https://example.com"
        };

        // Act
        var result = await _linkService.CreateAsync(newLink);

        // Assert
        result.Should().NotBeNull();
        result!.Slug.Should().Be(customSlug);
        _mockSlugGenerator.Verify(g => g.GenerateSlug(), Times.Never);
    }

    [Test]
    public async Task CreateAsync_ShouldReturnNullForCustomSlugConflict()
    {
        // Arrange
        var existingSlug = "custom1";
        _context.Links.Add(new Link
        {
            Slug = existingSlug,
            Domain = "test.com",
            DestinationUrl = "https://existing.com"
        });
        await _context.SaveChangesAsync();

        var newLink = new Link
        {
            Slug = existingSlug, // Custom slug that already exists
            Domain = "test.com",
            DestinationUrl = "https://newlink.com"
        };

        // Act
        var result = await _linkService.CreateAsync(newLink);

        // Assert
        result.Should().BeNull("custom slug conflicts should return null without retry");
    }

    [Test]
    public async Task CreateAsync_ShouldAllowSameSlugOnDifferentDomains()
    {
        // Arrange
        var slug = "sameslug";
        _mockSlugGenerator.Setup(g => g.GenerateSlug()).Returns(slug);

        // Add domain2
        _context.Domains.Add(new Domain { Host = "other.com", IsActive = true });
        await _context.SaveChangesAsync();

        // Add link on first domain
        _context.Links.Add(new Link
        {
            Slug = slug,
            Domain = "test.com",
            DestinationUrl = "https://test.com"
        });
        await _context.SaveChangesAsync();

        var newLink = new Link
        {
            Slug = slug, // Same slug but different domain
            Domain = "other.com",
            DestinationUrl = "https://other.com"
        };

        // Act
        var result = await _linkService.CreateAsync(newLink);

        // Assert
        result.Should().NotBeNull("same slug on different domain should be allowed");
        result!.Slug.Should().Be(slug);
    }

    [Test]
    public async Task CreateAsync_ShouldSetCreatedAtAndIsActive()
    {
        // Arrange
        _mockSlugGenerator.Setup(g => g.GenerateSlug()).Returns("test12");
        var before = DateTime.UtcNow;

        var newLink = new Link
        {
            Slug = "",
            Domain = "test.com",
            DestinationUrl = "https://example.com"
        };

        // Act
        var result = await _linkService.CreateAsync(newLink);

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
        result.CreatedAt.Should().BeOnOrAfter(before);
    }

    // Note: DbUpdateException retry logic (max 5 retries) cannot be tested with InMemoryDatabase
    // as it doesn't enforce unique constraints. This should be tested in integration tests with MySQL.
}
