namespace TextileConnect.Application.Invoicing;

/// <summary>The two letter state codes of the master data and the two digit state codes of the GST system (the first two digits of every GSTIN).</summary>
public static class GstStateCodes
{
    private static readonly Dictionary<string, string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JK"] = "01", ["HP"] = "02", ["PB"] = "03", ["CH"] = "04", ["UK"] = "05", ["HR"] = "06", ["DL"] = "07", ["RJ"] = "08", ["UP"] = "09", ["BR"] = "10",
        ["SK"] = "11", ["AR"] = "12", ["NL"] = "13", ["MN"] = "14", ["MZ"] = "15", ["TR"] = "16", ["ML"] = "17", ["AS"] = "18", ["WB"] = "19", ["JH"] = "20",
        ["OD"] = "21", ["CG"] = "22", ["MP"] = "23", ["GJ"] = "24", ["DH"] = "26", ["MH"] = "27", ["KA"] = "29", ["GA"] = "30", ["LD"] = "31", ["KL"] = "32",
        ["TN"] = "33", ["PY"] = "34", ["AN"] = "35", ["TS"] = "36", ["AP"] = "37", ["LA"] = "38",
    };

    /// <summary>The GST code for a state code such as "RJ" (08), or null when the state is not known.</summary>
    public static string? For(string? stateCode) => stateCode is not null && Codes.TryGetValue(stateCode, out var code) ? code : null;

    public static IReadOnlyCollection<string> KnownStates => Codes.Keys;
}
