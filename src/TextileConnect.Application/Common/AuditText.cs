using System.Globalization;

namespace TextileConnect.Application.Common;

/// <summary>Number formats for the details text of audit rows, the same whatever the server language is.</summary>
public static class AuditText
{
    public static string Money(decimal rupees) => rupees.ToString("0.00", CultureInfo.InvariantCulture);
}
