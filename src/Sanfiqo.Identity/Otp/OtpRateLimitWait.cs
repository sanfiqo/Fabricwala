namespace Sanfiqo.Identity;

public static class OtpRateLimitWait
{
    /// <summary>Time until fewer than <paramref name="limit"/> of the send times fall inside the sliding window (zero when the limit is not reached).</summary>
    public static TimeSpan For(IEnumerable<DateTime> sentAtUtc, TimeSpan window, int limit, DateTime utcNow)
    {
        var inWindow = sentAtUtc.Where(x => x > utcNow - window).OrderBy(x => x).ToArray();
        return inWindow.Length < limit ? TimeSpan.Zero : inWindow[inWindow.Length - limit] + window - utcNow;
    }
}
