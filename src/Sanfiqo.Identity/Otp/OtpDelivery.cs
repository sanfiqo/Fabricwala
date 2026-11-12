using System.Collections.Concurrent;
using Sanfiqo.Identity;

namespace Sanfiqo.Identity;

/// <summary>In-memory delivery capture for local Development/test only. It has no network surface.</summary>
public sealed class FakeOtpDelivery : IOtpDelivery, ILocalOtpInspection
{
    private sealed record CapturedCode(string Value, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<Guid, CapturedCode> _codes = new();

    public Task SendAsync(Guid challengeId, string mobileE164, string code, DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _codes[challengeId] = new CapturedCode(code, expiresAt);
        return Task.CompletedTask;
    }

    public bool TryGetCode(Guid challengeId, out string code)
    {
        code = string.Empty;
        if (!_codes.TryGetValue(challengeId, out var captured)) return false;
        if (captured.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _codes.TryRemove(challengeId, out _);
            return false;
        }
        code = captured.Value;
        return true;
    }
}

public sealed class UnconfiguredOtpDelivery : IOtpDelivery
{
    public Task SendAsync(Guid challengeId, string mobileE164, string code, DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
        => Task.FromException(new OtpDeliveryUnavailableException());
}
