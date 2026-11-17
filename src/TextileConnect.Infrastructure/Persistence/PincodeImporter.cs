using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;

namespace TextileConnect.Infrastructure.Persistence;

public sealed record PincodeImportResult(int RowsRead, int RowsSkipped, int PincodesInFile, int PincodesAdded, int PincodesAlreadyKnown, int CitiesAdded, IReadOnlyDictionary<string, int> UnknownStates);

/// <summary>
/// Loads a post office directory (one row per post office, the format of the India Post "All India Pincode Directory") into states, cities and pincodes.
/// A district becomes a city. A pincode that is already in the database is left as it is, so addresses and test data never move; running it again adds nothing.
/// Needed columns (any order, any capital letters): pincode, district (or districtname), statename (or state). The others are ignored.
/// </summary>
public static class PincodeImporter
{
    private const int Batch = 2000;

    private static readonly Dictionary<string, string[]> HeaderNames = new()
    {
        ["pincode"] = ["pincode", "pin code", "pin"],
        ["district"] = ["district", "districtname", "district name"],
        ["state"] = ["statename", "state name", "state"]
    };

    /// <summary>State names as they appear in the directory (capitals, "&amp;" or "and", old names) to the state codes of IndiaMasterData.</summary>
    private static readonly Dictionary<string, string> StateCodes = BuildStateCodes();

    private static Dictionary<string, string> BuildStateCodes()
    {
        var codes = IndiaMasterData.States.ToDictionary(x => Normalize(x.Name), x => x.Code);
        foreach (var (alias, code) in new[]
        {
            ("Orissa", "OD"), ("Uttaranchal", "UK"), ("Pondicherry", "PY"), ("Jammu & Kashmir", "JK"), ("Andaman & Nicobar Islands", "AN"),
            ("Dadra & Nagar Haveli", "DH"), ("Daman & Diu", "DH"), ("Dadra and Nagar Haveli", "DH"), ("Daman and Diu", "DH"), ("NCT of Delhi", "DL"),
            ("Chattisgarh", "CG"), ("Telangana State", "TS"), ("The Dadra And Nagar Haveli And Daman And Diu", "DH")
        })
            codes[Normalize(alias)] = code;
        return codes;
    }

    private static string Normalize(string value)
        => string.Join(' ', value.Replace("&", " and ", StringComparison.Ordinal).ToLowerInvariant().Split([' ', '\t', '.', ','], StringSplitOptions.RemoveEmptyEntries));

    private static string TitleCase(string value) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(string.Join(' ', value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)));

    public static async Task<PincodeImportResult> ImportAsync(MarketplaceDbContext db, TextReader csv, CancellationToken cancellationToken = default)
    {
        // 1. read: for every pincode count which (state, district) its post offices name, and keep the most common one
        var header = ReadRow(csv);
        if (header is null) throw new InvalidDataException("The file is empty.");
        int Find(string key) => Array.FindIndex(header, h => HeaderNames[key].Contains(h.Trim().ToLowerInvariant()));
        int pin = Find("pincode"), district = Find("district"), state = Find("state");
        if (pin < 0 || district < 0 || state < 0)
            throw new InvalidDataException("The first row must name the columns pincode, district (or districtname) and statename (or state).");

        var votes = new Dictionary<string, Dictionary<(string State, string District), int>>(StringComparer.Ordinal);
        var unknownStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int rows = 0, skipped = 0;
        for (var row = ReadRow(csv); row is not null; row = ReadRow(csv))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows++;
            if (row.Length <= Math.Max(pin, Math.Max(district, state))) { skipped++; continue; }
            var pincode = row[pin].Trim();
            var districtName = row[district].Trim();
            var stateName = row[state].Trim();
            if (pincode.Length != 6 || pincode[0] == '0' || !pincode.All(char.IsAsciiDigit) || districtName.Length == 0 || districtName.Length > 100 || stateName.Length == 0) { skipped++; continue; }
            if (!StateCodes.TryGetValue(Normalize(stateName), out var code))
            {
                unknownStates[stateName] = unknownStates.GetValueOrDefault(stateName) + 1;
                skipped++;
                continue;
            }
            if (!votes.TryGetValue(pincode, out var perPincode)) votes[pincode] = perPincode = [];
            var key = (code, TitleCase(districtName));
            perPincode[key] = perPincode.GetValueOrDefault(key) + 1;
        }

        // 2. cities: one per (state, district), reusing a city that already has that name in the state
        var statesByCode = await db.States.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        var cities = (await db.Cities.AsNoTracking().ToListAsync(cancellationToken)).GroupBy(x => x.StateId)
            .ToDictionary(g => g.Key, g => g.GroupBy(c => c.Name.ToLowerInvariant()).ToDictionary(c => c.Key, c => c.OrderBy(x => x.Id).First().Id));
        var known = (await db.PostalCodes.AsNoTracking().Select(x => x.Pincode).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var citiesAdded = 0;
        var added = 0;
        var alreadyKnown = 0;
        var pending = new List<PostalCode>();
        foreach (var (pincode, perPincode) in votes.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (known.Contains(pincode)) { alreadyKnown++; continue; }
            var (stateCode, districtName) = perPincode.OrderByDescending(x => x.Value).ThenBy(x => x.Key.District, StringComparer.Ordinal).First().Key;
            if (!statesByCode.TryGetValue(stateCode, out var stateId)) { skipped++; continue; } // the state is not in the database (the startup seed has not run)
            if (!cities.TryGetValue(stateId, out var inState)) cities[stateId] = inState = [];
            if (!inState.TryGetValue(districtName.ToLowerInvariant(), out var cityId))
            {
                var city = new City { StateId = stateId, Name = districtName, IsDeliveryZone = true, IsSourcingHub = IndiaMasterData.SourcingHubs.Contains(districtName) };
                db.Cities.Add(city);
                await db.SaveChangesAsync(cancellationToken);
                inState[districtName.ToLowerInvariant()] = cityId = city.Id;
                citiesAdded++;
            }
            pending.Add(new PostalCode { Pincode = pincode, CityId = cityId });
            added++;
            if (pending.Count >= Batch) { await FlushAsync(db, pending, cancellationToken); }
        }
        await FlushAsync(db, pending, cancellationToken);
        db.ChangeTracker.AutoDetectChangesEnabled = true;
        return new PincodeImportResult(rows, skipped, votes.Count, added, alreadyKnown, citiesAdded, unknownStates);
    }

    private static async Task FlushAsync(MarketplaceDbContext db, List<PostalCode> pending, CancellationToken cancellationToken)
    {
        if (pending.Count == 0) return;
        db.PostalCodes.AddRange(pending);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        pending.Clear();
    }

    /// <summary>One row of a comma separated file: quoted fields may hold commas, doubled quotes and line breaks. Null at the end of the file; blank lines are skipped.</summary>
    public static string[]? ReadRow(TextReader reader)
    {
        while (true)
        {
            var first = reader.Read();
            if (first < 0) return null;
            if (first is '\r' or '\n') continue;
            var fields = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            var c = first;
            while (true)
            {
                if (quoted)
                {
                    if (c < 0) break;
                    if (c == '"')
                    {
                        if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                        else quoted = false;
                    }
                    else field.Append((char)c);
                }
                else if (c == '"' && field.Length == 0) quoted = true;
                else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
                else if (c is '\r' or '\n') { if (c == '\r' && reader.Peek() == '\n') reader.Read(); break; }
                else if (c < 0) break;
                else field.Append((char)c);
                c = reader.Read();
            }
            fields.Add(field.ToString());
            return fields.ToArray();
        }
    }
}
