using FluentAssertions;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Infrastructure;

public class SlugGeneratorTests
{
    [Test]
    public void GenerateSlug_ShouldReturnStringOfSpecifiedLength()
    {
        // Arrange
        var generator = new SlugGenerator();
        int length = 10;

        // Act
        var slug = generator.GenerateSlug(length);

        // Assert
        slug.Should().NotBeNullOrEmpty();
        slug.Length.Should().Be(length);
    }

    [Test]
    public void GenerateSlug_ShouldReturnDifferentSlugs()
    {
        // Arrange
        var generator = new SlugGenerator();

        // Act
        var slug1 = generator.GenerateSlug();
        var slug2 = generator.GenerateSlug();

        // Assert
        slug1.Should().NotBe(slug2);
    }
}
