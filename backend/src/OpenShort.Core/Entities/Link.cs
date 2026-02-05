using System.ComponentModel.DataAnnotations;

namespace OpenShort.Core.Entities;

public class Link
{
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Slug { get; set; }

    [Required]
    [MaxLength(2048)]
    public required string DestinationUrl { get; set; }

    [Required]
    [MaxLength(255)]
    public required string Domain { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public RedirectType RedirectType { get; set; } = RedirectType.Permanent;

    [MaxLength(100)]
    public string? Title { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }

    public int ClickCount { get; set; }

    public DateTime? LastAccessedAt { get; set; }
}

public enum RedirectType
{
    Permanent = 301,
    Temporary = 302
}
