using TextileConnect.Domain.Businesses;

namespace TextileConnect.Api;

public sealed record BusinessDocumentResponse(long Id, BusinessDocumentType DocumentType,
    BusinessDocumentStatus Status, string? ReviewNote, string Url);
