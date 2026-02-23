namespace OpenShort.Core.Entities;

public class ClickEvent
{
    public required string Slug { get; set; }
    public required string Domain { get; set; }
    public DateTime Timestamp { get; set; }
}
