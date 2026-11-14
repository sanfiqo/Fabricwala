namespace TextileConnect.Application.Common;

/// <summary>A business-rule failure with a stable, safe code (see docs/API_SCREENS.md error codes).</summary>
public sealed class RuleViolationException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
