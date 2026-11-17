using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>
/// <c>dotnet run --project src/TextileConnect.Api -- import-pincodes &lt;file.csv&gt;</c>
/// Loads a post office directory into the database named by the normal configuration (user-secrets, environment variables), then exits. It does not start the web server.
/// The database must already be migrated. Safe to run again: pincodes that exist are left alone.
/// </summary>
public static class PincodeImportCommand
{
    public const string Name = "import-pincodes";

    public static bool IsRequested(string[] args) => args.Length > 0 && string.Equals(args[0], Name, StringComparison.OrdinalIgnoreCase);

    /// <returns>0 when it worked, 2 when the file or the command was wrong, 1 when the import failed.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output, CancellationToken cancellationToken = default)
    {
        if (args.Length != 2) { await output.WriteLineAsync($"Usage: {Name} <file.csv>   (columns: pincode, district, statename)"); return 2; }
        if (!File.Exists(args[1])) { await output.WriteLineAsync($"File not found: {args[1]}"); return 2; }
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            await MarketplaceSeed.SeedAsync(db, cancellationToken); // the states the file refers to
            using var reader = new StreamReader(args[1], System.Text.Encoding.UTF8);
            var result = await PincodeImporter.ImportAsync(db, reader, cancellationToken);
            await output.WriteLineAsync($"Rows read: {result.RowsRead}. Skipped: {result.RowsSkipped}. Pincodes in the file: {result.PincodesInFile}.");
            await output.WriteLineAsync($"Pincodes added: {result.PincodesAdded}. Already known: {result.PincodesAlreadyKnown}. Cities (districts) added: {result.CitiesAdded}.");
            foreach (var (state, count) in result.UnknownStates.OrderByDescending(x => x.Value))
                await output.WriteLineAsync($"Skipped {count} rows with the unknown state '{state}'.");
            return 0;
        }
        catch (InvalidDataException exception) { await output.WriteLineAsync(exception.Message); return 2; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await output.WriteLineAsync($"The import failed: {exception.Message}");
            return 1;
        }
    }
}
