namespace TextileConnect.Application.Features;

/// <summary>
/// Parts of the marketplace that the owner switches on when the people and the processes around them are ready (support, returns, grievance, legal text). A feature that is off answers 404
/// and is hidden by the client. The switch is the platform setting "Feature.&lt;name&gt;" ("on" or "off"); a missing setting means off.
/// </summary>
public static class Feature
{
    public const string Rfq = "Rfq";

    public static readonly IReadOnlyList<string> All = [Rfq];

    public static string SettingKey(string feature) => $"Feature.{feature}";
}

public interface IFeatureFlags
{
    Task<bool> IsOnAsync(string feature, CancellationToken cancellationToken = default);
    Task SetAsync(string feature, bool on, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, bool>> AllAsync(CancellationToken cancellationToken = default);
}
