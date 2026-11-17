using System.Text.RegularExpressions;

namespace TextileConnect.Application.Businesses;

/// <summary>
/// GSTIN check without the GST portal: the 15-character shape, a state code the GST system knows, and the check character.
/// It catches typos and invented numbers. It does not prove that the number is registered; that needs the GST portal.
/// </summary>
public static partial class GstinRules
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")]
    private static partial Regex Shape();

    /// <summary>True for a GSTIN written in capital letters whose shape, state code and check character are right.</summary>
    public static bool IsValid(string? gstin)
        => gstin is not null && Shape().IsMatch(gstin) && IsKnownStateCode(gstin) && gstin[14] == CheckCharacter(gstin);

    /// <summary>State codes 01 to 38, 97 (other territory) and 99 (centre jurisdiction).</summary>
    private static bool IsKnownStateCode(string gstin)
    {
        var code = int.Parse(gstin[..2], System.Globalization.CultureInfo.InvariantCulture);
        return code is >= 1 and <= 38 or 97 or 99;
    }

    /// <summary>The 15th character, from the first 14: each character is worth its place in 0-9A-Z, odd places count once and even places twice, mod 36.</summary>
    public static char CheckCharacter(string gstin)
    {
        var total = 0;
        for (var i = 0; i < 14; i++)
        {
            var product = Alphabet.IndexOf(gstin[i]) * (i % 2 == 0 ? 1 : 2);
            total += product / 36 + product % 36;
        }
        return Alphabet[(36 - total % 36) % 36];
    }
}
