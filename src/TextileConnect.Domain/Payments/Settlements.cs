namespace TextileConnect.Domain.Payments;

public static class Settlements
{
    /// <summary>ReleaseAfter of a held settlement whose release date is not known yet (not shipped or delivered).</summary>
    public static readonly DateTime NoReleaseDate = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
    public static bool HasReleaseDate(DateTime releaseAfter) => releaseAfter < NoReleaseDate;
}
