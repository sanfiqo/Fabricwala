namespace TextileConnect.Application.Files;

public interface IFileStorage
{
    Task<string> SavePrivateAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<Stream> OpenPrivateAsync(string fileKey, CancellationToken cancellationToken = default);

    /// <summary>True when the storage can be reached and written. Used by the readiness check; a real storage should try a small harmless request.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
