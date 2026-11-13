namespace Sanfiqo.Identity;

public sealed class SanfiqoIdentityOptions
{
    public const string SectionName = "SanfiqoIdentity";

    public string CookieName { get; set; } = ".AspNetCore.TextileConnect.Identity";
    public TimeSpan CookieLifetime { get; set; } = TimeSpan.FromHours(8);
    public bool CookieSlidingExpiration { get; set; }
    public bool RequireSecureCookie { get; set; } = true;
    public string AntiforgeryHeaderName { get; set; } = "X-CSRF-TOKEN";
    public string LookupHmacKeyConfiguration { get; set; } = "UserContacts:LookupHmacKey";
    public string OtpStorageHmacKeyConfiguration { get; set; } = "Otp:StorageHmacKey";
}
