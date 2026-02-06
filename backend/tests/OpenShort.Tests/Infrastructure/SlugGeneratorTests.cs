using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using OpenShort.Core;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Infrastructure;

public class SlugGeneratorTests
{
    private SlugGenerator _generator = null!;
    private Mock<IOptions<SlugSettings>> _mockOptions = null!;
    
    [SetUp]
    public void Setup()
    {
        _mockOptions = new Mock<IOptions<SlugSettings>>();
        _mockOptions.Setup(o => o.Value).Returns(new SlugSettings { Length = 6, MaxRetries = 5 });
        _generator = new SlugGenerator(_mockOptions.Object);
    }
    
    [Test]
    public void GenerateSlug_ShouldReturnStringOfConfiguredLength()
    {
        // Act
        var slug = _generator.GenerateSlug();

        // Assert
        slug.Should().NotBeNullOrEmpty();
        slug.Length.Should().Be(6);
    }

    [Test]
    public void GenerateSlug_ShouldReturnDifferentSlugs()
    {
        // Act
        var slug1 = _generator.GenerateSlug();
        var slug2 = _generator.GenerateSlug();

        // Assert
        slug1.Should().NotBe(slug2);
    }
    
    [Test]
    public void GenerateSlug_ShouldOnlyContainLowercaseAlphanumericCharacters()
    {
        // Act
        var slug = _generator.GenerateSlug();

        // Assert
        slug.Should().MatchRegex("^[a-z0-9]+$");
    }
}
