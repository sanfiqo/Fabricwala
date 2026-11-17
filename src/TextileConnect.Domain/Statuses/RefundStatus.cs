namespace TextileConnect.Domain;

/// <summary>Requested (waiting for the gateway), Processed or Failed.</summary>
public static class RefundStatus
{
    public const string Requested = "Requested";
    public const string Processed = "Processed";
    public const string Failed = "Failed";

    public static readonly IReadOnlyList<string> All = [Requested, Processed, Failed];
}
