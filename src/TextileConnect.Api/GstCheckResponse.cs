namespace TextileConnect.Api;

public sealed record GstCheckResponse(bool Provided, bool? FormatValid, string Status);
