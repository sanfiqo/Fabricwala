namespace TextileConnect.Application.Common;

/// <summary>Writes an audit row for the current caller. The row is added to the unit of work and saved by the caller's own SaveChanges, so it is stored together with the change it describes.</summary>
public interface IAuditTrail
{
    /// <param name="action">Short dotted name such as "payment.paid" or "settlement.released" (up to 64 characters).</param>
    /// <param name="details">A short plain note such as an amount or a status. Never a secret, an OTP, a document key or a request body.</param>
    void Record(string action, string entityName, object? entityId = null, string? details = null);
}
