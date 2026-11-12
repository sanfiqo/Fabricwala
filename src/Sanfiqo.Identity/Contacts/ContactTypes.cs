using System.Text.RegularExpressions;

namespace Sanfiqo.Identity;

public enum UserContactKind : byte
{
    Mobile = 1,
    Email = 2
}

public sealed record NormalizedUserContact(UserContactKind Kind, string Value);

public static class UserContactNormalizer
{
    public static bool TryNormalize(UserContactKind kind, string? value,
        out NormalizedUserContact normalized)
    {
        normalized = default!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        if (kind == UserContactKind.Mobile)
        {
            var mobile = Regex.Replace(trimmed, "[\\s()-]", "");
            if (!mobile.StartsWith('+') || mobile.Length < 8 || mobile.Length > 16
                || mobile.Skip(1).Any(c => c is < '0' or > '9')) return false;
            normalized = new(kind, mobile);
            return true;
        }
        if (kind == UserContactKind.Email && trimmed.Contains('@')
            && trimmed.Length <= 320)
        {
            normalized = new(kind, trimmed.ToUpperInvariant());
            return true;
        }
        return false;
    }
}
