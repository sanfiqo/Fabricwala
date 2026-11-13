namespace TextileConnect.Domain.Community;
public sealed class Quotation { public long Id { get; set; } public long EnquiryId { get; set; } public long ManufacturerBusinessId { get; set; } public decimal UnitPrice { get; set; } public int ValidDays { get; set; } public string Status { get; set; } = "Pending"; }
