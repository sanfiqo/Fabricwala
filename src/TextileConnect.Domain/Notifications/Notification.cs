namespace TextileConnect.Domain.Notifications;

public sealed class Notification
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
