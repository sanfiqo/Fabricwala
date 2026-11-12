namespace TextileConnect.Domain.Businesses;

public sealed class State
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
}
