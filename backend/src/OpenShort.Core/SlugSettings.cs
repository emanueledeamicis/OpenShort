namespace OpenShort.Core;

/// <summary>
/// Configuration options for slug generation.
/// </summary>
public class SlugSettings
{
    /// <summary>
    /// Length of auto-generated slugs (default: 6).
    /// </summary>
    public int Length { get; set; } = 6;
    
    /// <summary>
    /// Maximum number of retry attempts when a slug collision occurs (default: 5).
    /// </summary>
    public int MaxRetries { get; set; } = 5;
}
