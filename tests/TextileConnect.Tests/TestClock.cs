namespace TextileConnect.Tests;

/// <summary>Controllable clock: starts at the real time and only moves when a test advances it.</summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan by) => now += by;
}
