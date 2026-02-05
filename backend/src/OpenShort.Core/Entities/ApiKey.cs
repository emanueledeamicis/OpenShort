namespace OpenShort.Core.Entities;

public class ApiKey
{
    public long Id { get; set; }
    public string Name { get; set; } = "Default API Key";
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty; // First 8 chars for identification
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? LastUsedAt { get; set; }
}
