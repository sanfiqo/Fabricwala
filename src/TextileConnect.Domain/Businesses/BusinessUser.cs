namespace TextileConnect.Domain.Businesses;

public enum BusinessMemberRole { Owner, Staff }

public sealed class BusinessUser
{
    public long BusinessId { get; set; }
    public required string UserId { get; set; }
    public BusinessMemberRole Role { get; set; }
    public bool IsPrimary { get; set; }
}
