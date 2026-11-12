using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sanfiqo.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Extensions;
using Microsoft.Extensions.Configuration;

namespace Sanfiqo.Identity;

/// <summary>
/// Contact values and proof tickets use independent Data Protection purposes. Search keys use
/// a separate configured HMAC secret and are never derived from the Data Protection key ring.
/// </summary>
public sealed class UserContactCryptography : IUserContactProofProtector
{
    private const string LookupKeyConfiguration = "UserContacts:LookupHmacKey";
    private readonly IDataProtector _valueProtector;
    private readonly ITimeLimitedDataProtector _proofProtector;
    private readonly byte[] _lookupKey;

    public UserContactCryptography(IDataProtectionProvider protection, IConfiguration configuration)
    {
        _valueProtector = protection.CreateProtector("TextileConnect.UserContacts.StoredValue.v1");
        _proofProtector = protection.CreateProtector("TextileConnect.UserContacts.VerificationProof.v1")
            .ToTimeLimitedDataProtector();
        var configured = configuration[LookupKeyConfiguration];
        try { _lookupKey = Convert.FromBase64String(configured ?? string.Empty); }
        catch (FormatException) { throw new InvalidOperationException("User contact lookup-key configuration is invalid."); }
        if (_lookupKey.Length < 32)
            throw new InvalidOperationException("User contact lookup-key configuration is unavailable or too short.");
    }

    public byte[] CreateLookupKey(NormalizedUserContact contact)
    {
        var input = Encoding.UTF8.GetBytes($"user-contact-lookup:v1\0{(byte)contact.Kind}\0{contact.Value}");
        return HMACSHA256.HashData(_lookupKey, input);
    }

    public byte[] ProtectContactValue(string normalizedValue)
        => Encoding.UTF8.GetBytes(_valueProtector.Protect(normalizedValue));

    public string UnprotectContactValue(byte[] protectedValue)
        => _valueProtector.Unprotect(Encoding.UTF8.GetString(protectedValue));

    internal string Protect(UserContactVerificationProof proof)
    {
        if (proof.ProofId == Guid.Empty || proof.SourceVerificationId == Guid.Empty
            || proof.Purpose != UserContactProofPurpose.Value || proof.ExpiresAtUtc <= proof.IssuedAtUtc)
            throw new ArgumentException("The user contact proof is invalid.", nameof(proof));
        var lifetime = proof.ExpiresAtUtc - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero) throw new ArgumentException("The user contact proof is expired.", nameof(proof));
        return _proofProtector.Protect(JsonSerializer.Serialize(proof), lifetime);
    }

    public bool TryUnprotect(string? token, out UserContactVerificationProof? proof)
    {
        proof = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192) return false;
        try
        {
            var payload = _proofProtector.Unprotect(token, out var expiration);
            var candidate = JsonSerializer.Deserialize<UserContactVerificationProof>(payload);
            if (candidate is null || expiration <= DateTimeOffset.UtcNow || candidate.ExpiresAtUtc <= DateTimeOffset.UtcNow
                || candidate.Purpose != UserContactProofPurpose.Value
                || !UserContactNormalizer.TryNormalize(candidate.Kind, candidate.NormalizedValue, out var normalized)
                || !string.Equals(normalized.Value, candidate.NormalizedValue, StringComparison.Ordinal))
                return false;
            proof = candidate;
            return true;
        }
        catch (CryptographicException) { return false; }
        catch (JsonException) { return false; }
    }
}
