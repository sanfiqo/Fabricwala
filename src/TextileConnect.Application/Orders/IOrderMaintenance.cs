namespace TextileConnect.Application.Orders;

/// <summary>Time-based housekeeping run by a background worker (and directly in tests with a fake clock).</summary>
public interface IOrderMaintenance
{
    /// <summary>
    /// Cancels paid seller orders whose acceptance window has passed (full refund, stock released) and unpaid orders
    /// that were never paid in time (stock released). Returns how many seller orders were cancelled.
    /// </summary>
    Task<int> ExpireAsync(CancellationToken cancellationToken = default);
}
