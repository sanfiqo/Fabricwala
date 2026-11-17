using System.Text.RegularExpressions;

namespace TextileConnect.Application.Catalogue;

/// <summary>
/// Decides how a search text is matched. A text made only of plain words of three letters or more is searched with the MySQL full-text index
/// (every word must start a word in the title, description or material). Anything else, such as one or two letters, a word the full-text index
/// ignores, or non-English letters, falls back to "contains" matching so that short and unusual searches still find things.
/// </summary>
public static partial class ProductTextSearch
{
    public const int MinFullTextWordLength = 3;
    public const int MaxFullTextWords = 8;

    /// <summary>Words that InnoDB leaves out of its full-text index. A search for only these would find nothing.</summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "about", "are", "com", "for", "from", "how", "that", "the", "this", "was", "what", "when", "where", "who", "will", "with", "und", "www"
    };

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NotAWord();

    /// <summary>Like is the trimmed lower-case text for "contains" matching (null for an empty search). BooleanQuery is the MySQL boolean-mode text, or null when the full-text index cannot be used.</summary>
    public sealed record Plan(string? Like, string? BooleanQuery);

    public static Plan For(string? text)
    {
        var trimmed = text?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed)) return new Plan(null, null);
        var words = NotAWord().Split(trimmed).Where(x => x.Length > 0).ToList();
        var fullText = words.Count is > 0 and <= MaxFullTextWords
            && words.All(x => x.Length >= MinFullTextWordLength && x.All(char.IsAsciiLetterOrDigit) && !Stopwords.Contains(x));
        // Every word is required and may be the start of a longer word ("jack" finds "jacket"). Only letters and digits reach the query, so no operator can be injected.
        return new Plan(trimmed, fullText ? string.Join(' ', words.Select(x => $"+{x}*")) : null);
    }
}
