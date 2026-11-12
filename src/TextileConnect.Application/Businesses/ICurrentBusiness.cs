using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TextileConnect.Application.Businesses;

public interface ICurrentBusiness
{
    long? BusinessId { get; }
    long RequireBusinessId();
}

public sealed class CurrentBusiness(IHttpContextAccessor httpContextAccessor) : ICurrentBusiness
{
    public long? BusinessId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue("business_id");
            return long.TryParse(value, out var businessId) && businessId > 0 ? businessId : null;
        }
    }

    public long RequireBusinessId() => BusinessId ?? throw new InvalidOperationException("A business context is required.");
}
