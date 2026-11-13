namespace Sanfiqo.Identity;

using System.Security.Cryptography;
using System.Text;
using System.Globalization;

/// <summary>Approved user challenge rules. IP thresholds must be configured before issuing challenges.</summary>
public sealed class OtpPolicy
{
    public string? StorageHmacKey { get; set; }
    public int CodeDigits { get; set; } = 6;
    public TimeSpan ChallengeLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
    public int MaximumWrongAttempts { get; set; } = 5;
    public int MobileSendsPer15Minutes { get; set; } = 3;
    public int MobileSendsPer24Hours { get; set; } = 10;
    public int? IpSendsPer15Minutes { get; set; }
    public int? IpSendsPer24Hours { get; set; }
    public string? DevFixedCode { get; set; }
    public bool RelaxRateLimits { get; set; }

    public string CreateCode(string environmentName)
    {
        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            && DevFixedCode is { Length: 6 } fixedCode
            && fixedCode.All(char.IsDigit))
            return fixedCode;

        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<string> Validate(bool requireIpLimits = false)
    {
        var errors = new List<string>();
        if (CodeDigits != 6) errors.Add("CodeDigits must remain 6.");
        if (ChallengeLifetime != TimeSpan.FromMinutes(5)) errors.Add("ChallengeLifetime must remain 5 minutes.");
        if (ResendCooldown != TimeSpan.FromSeconds(60)) errors.Add("ResendCooldown must remain 60 seconds.");
        if (MaximumWrongAttempts != 5) errors.Add("MaximumWrongAttempts must remain 5.");
        if (MobileSendsPer15Minutes != 3) errors.Add("MobileSendsPer15Minutes must remain 3.");
        if (MobileSendsPer24Hours != 10) errors.Add("MobileSendsPer24Hours must remain 10.");
        if (IpSendsPer15Minutes is <= 0) errors.Add("IpSendsPer15Minutes must be positive when configured.");
        if (IpSendsPer24Hours is <= 0) errors.Add("IpSendsPer24Hours must be positive when configured.");
        if (requireIpLimits && (IpSendsPer15Minutes is null || IpSendsPer24Hours is null))
            errors.Add("Both IP send limits must be configured before issuing challenges.");
        return errors;
    }

    public byte[] Hash(string value)
    {
        if (string.IsNullOrWhiteSpace(StorageHmacKey))
            throw new InvalidOperationException("Otp:StorageHmacKey is not configured.");
        byte[] key;
        try { key = Convert.FromBase64String(StorageHmacKey); }
        catch (FormatException) { key = Encoding.UTF8.GetBytes(StorageHmacKey); }
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }
}
