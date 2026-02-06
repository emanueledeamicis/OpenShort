namespace OpenShort.Core.Interfaces;

/// <summary>
/// Service for generating URL-safe slugs.
/// </summary>
public interface ISlugGenerator
{
    /// <summary>
    /// Generates a unique URL-safe slug.
    /// </summary>
    /// <returns>A randomly generated slug.</returns>
    string GenerateSlug();
}
