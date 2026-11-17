namespace TextileConnect.Application.Invoicing;

/// <summary>The Indian financial year runs from 1 April to 31 March and is judged by the date in India (UTC+5:30), not in UTC.</summary>
public static class FinancialYear
{
    private static readonly TimeSpan India = TimeSpan.FromMinutes(330);

    /// <summary>Four digits: 1 April 2026 to 31 March 2027 is "2627".</summary>
    public static string Code(DateTime utc)
    {
        var local = utc.Add(India);
        var start = local.Month >= 4 ? local.Year : local.Year - 1;
        return $"{start % 100:D2}{(start + 1) % 100:D2}";
    }

    public static DateOnly IndiaDate(DateTime utc) => DateOnly.FromDateTime(utc.Add(India));
}
