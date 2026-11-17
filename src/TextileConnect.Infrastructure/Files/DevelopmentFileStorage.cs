using TextileConnect.Application.Files;
using Microsoft.Extensions.Hosting;

namespace TextileConnect.Infrastructure.Files;

public sealed class DevelopmentFileStorage(IHostEnvironment environment) : IFileStorage
{
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "private-files");
    public async Task<string> SavePrivateAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Root);
        var key = $"{Guid.NewGuid():N}-{Path.GetFileName(fileName)}";
        await using var output = File.Create(Path.Combine(Root, key));
        await content.CopyToAsync(output, cancellationToken);
        return key;
    }
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try { Directory.CreateDirectory(Root); var probe = Path.Combine(Root, ".health"); File.WriteAllText(probe, "ok"); File.Delete(probe); return Task.FromResult(true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return Task.FromResult(false); }
    }
    public Task<Stream> OpenPrivateAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        var safeKey = Path.GetFileName(fileKey);
        if (!string.Equals(safeKey, fileKey, StringComparison.Ordinal)) throw new FileNotFoundException();
        Stream result = File.OpenRead(Path.Combine(Root, safeKey));
        return Task.FromResult(result);
    }
}
