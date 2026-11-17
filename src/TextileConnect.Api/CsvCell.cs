namespace TextileConnect.Api;

/// <summary>
/// A text value for a CSV file that people will open in a spreadsheet. A business can choose its own name, so the name is treated as hostile:
/// a cell that starts with = + - @ (or a tab or line break) would be run as a formula by Excel and Google Sheets, so it gets a leading apostrophe,
/// and a cell with a comma, a quote or a line break is put in quotes so that it cannot add columns or rows.
/// Use it for every text that a user typed; numbers and system values (ids, amounts, statuses) need no care.
/// </summary>
public static class CsvCell
{
    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
