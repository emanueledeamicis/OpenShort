namespace OpenShort.Tests;

/// <summary>
/// Time provider whose clock moves only when a test advances it.
/// </summary>
public class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow += duration;
}
