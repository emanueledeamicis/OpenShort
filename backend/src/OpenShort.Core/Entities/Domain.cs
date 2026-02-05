using System.ComponentModel.DataAnnotations;

namespace OpenShort.Core.Entities;

public class Domain
{
    public long Id { get; set; }

    [Required]
    [MaxLength(255)]
    public required string Host { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
