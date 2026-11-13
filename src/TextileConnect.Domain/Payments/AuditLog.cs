namespace TextileConnect.Domain.Payments;

public sealed class AuditLog { public long Id { get; set; } public string? UserId { get; set; } public required string Action { get; set; } public required string EntityName { get; set; } public string? EntityId { get; set; } public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; }
