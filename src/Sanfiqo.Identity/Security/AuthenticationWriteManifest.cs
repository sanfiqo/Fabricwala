namespace Sanfiqo.Identity;

/// <summary>
/// Optional test-harness hook. The normal host does not register an implementation.
/// Test manifests persist exact owned IDs before a corresponding synthetic write begins.
/// </summary>
public interface IMobileAuthenticationWriteManifest
{
    void TrackBeforeWrite(string recordKind, string recordId);
}

internal static class MobileAuthenticationWriteManifestExtensions
{
    public static void TrackBeforeWrite(this IEnumerable<IMobileAuthenticationWriteManifest> manifests,
        string recordKind, string recordId)
    {
        foreach (var manifest in manifests)
            manifest.TrackBeforeWrite(recordKind, recordId);
    }
}
