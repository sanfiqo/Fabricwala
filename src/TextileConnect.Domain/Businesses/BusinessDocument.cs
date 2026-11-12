namespace TextileConnect.Domain.Businesses;

public enum BusinessDocumentType { Gst, Pan, ShopLicence, Udyam }
public enum BusinessDocumentStatus { Pending, Accepted, Rejected }

public sealed class BusinessDocument
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public BusinessDocumentType DocumentType { get; set; }
    public required string FileKey { get; set; }
    public BusinessDocumentStatus Status { get; set; } = BusinessDocumentStatus.Pending;
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNote { get; set; }
}
