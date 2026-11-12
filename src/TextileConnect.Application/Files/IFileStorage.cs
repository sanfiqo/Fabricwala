namespace TextileConnect.Application.Files;

public interface IFileStorage
{
    Task<string> SavePrivateAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<Stream> OpenPrivateAsync(string fileKey, CancellationToken cancellationToken = default);
}
