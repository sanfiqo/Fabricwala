namespace TextileConnect.Domain.Payments;

public sealed class PayoutAccount { public long Id { get; set; } public long BusinessId { get; set; } public string Gateway { get; set; } = "Razorpay"; public required string GatewayLinkedAccountId { get; set; } public string Status { get; set; } = "Pending"; }
