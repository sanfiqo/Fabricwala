using Microsoft.AspNetCore.Identity;

namespace Sanfiqo.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
}
