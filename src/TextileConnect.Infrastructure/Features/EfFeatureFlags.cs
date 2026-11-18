using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Features;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Features;

/// <summary>The feature switches, kept as platform settings named "Feature.&lt;name&gt;" with the value "on" or "off". A missing or unknown value is off.</summary>
public sealed class EfFeatureFlags(MarketplaceDbContext db) : IFeatureFlags
{
    public async Task<bool> IsOnAsync(string feature, CancellationToken cancellationToken = default)
    {
        var key = Feature.SettingKey(feature);
        var value = await db.PlatformSettings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).SingleOrDefaultAsync(cancellationToken);
        return string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetAsync(string feature, bool on, CancellationToken cancellationToken = default)
    {
        if (!Feature.All.Contains(feature)) throw new ArgumentException("Unknown feature.", nameof(feature));
        var key = Feature.SettingKey(feature);
        var setting = await db.PlatformSettings.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting is null) db.PlatformSettings.Add(new PlatformSetting { Key = key, Value = on ? "on" : "off", UpdatedAtUtc = DateTime.UtcNow });
        else { setting.Value = on ? "on" : "off"; setting.UpdatedAtUtc = DateTime.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, bool>> AllAsync(CancellationToken cancellationToken = default)
    {
        var keys = Feature.All.Select(Feature.SettingKey).ToArray();
        var settings = await db.PlatformSettings.AsNoTracking().Where(x => keys.Contains(x.Key)).ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);
        return Feature.All.ToDictionary(x => x, x => settings.TryGetValue(Feature.SettingKey(x), out var value) && string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));
    }
}
