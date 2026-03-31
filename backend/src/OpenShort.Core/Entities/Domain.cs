using System.ComponentModel.DataAnnotations;

namespace OpenShort.Core.Entities;

public class Domain
{
    public long Id { get; set; }

    [Required]
    [MaxLength(255)]
    public required string Host { get; set; }

    public bool IsActive { get; set; } = true;

    public DomainNotFoundBehavior NotFoundBehavior { get; set; } = DomainNotFoundBehavior.OpenShortPage;

    [MaxLength(2048)]
    public string? NotFoundRedirectUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
