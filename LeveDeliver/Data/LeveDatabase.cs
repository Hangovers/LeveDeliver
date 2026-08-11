using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeveDeliver.Data;

/// <summary>
/// One levequest record from the embedded database.
/// </summary>
/// <remarks>
/// Data derived from xivganon/LEDE (LeveDelivery) DB.lua, MIT licensed.
/// See LeveDatabase.json header for the full attribution and license text.
/// </remarks>
public class LeveEntry
{
    public uint Id { get; set; }
    public string Name { get; set; } = "";
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public int Quantity { get; set; }
    public string Job { get; set; } = "";
    public int LevelRequired { get; set; }
    public uint LevemeteID { get; set; }
    public string LevemeteName { get; set; } = "";
    public uint TargetID { get; set; }
    public string TargetName { get; set; } = "";
    public bool Triple { get; set; }
}

/// <summary>
/// The embedded levequest database: leve RowId -> entry, plus name lookup.
/// Loaded at startup from the embedded LeveDatabase.json resource.
/// </summary>
public class LeveDatabase
{
    public IReadOnlyDictionary<uint, LeveEntry> ById { get; }
    public IReadOnlyDictionary<string, LeveEntry> ByName { get; }
    public IReadOnlyList<LeveEntry> All { get; }

    public LeveDatabase(IEnumerable<LeveEntry> entries)
    {
        this.All = entries.ToList();
        this.ById = this.All.ToDictionary(x => x.Id);
        var byName = new Dictionary<string, LeveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in this.All)
            byName.TryAdd(entry.Name, entry);
        this.ByName = byName;
    }

    public static LeveDatabase Load(IServiceProvider services)
    {
        Stream? stream = null;
        try
        {
            stream = typeof(LeveDatabase).Assembly.GetManifestResourceStream("LeveDeliver.Data.LeveDatabase.json");
            if (stream == null)
            {
                // Development fallback: file next to the plugin DLL.
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "LeveDatabase.json");
                if (System.IO.File.Exists(path))
                    stream = System.IO.File.OpenRead(path);
            }

            if (stream == null)
                throw new InvalidOperationException("LeveDatabase.json not found (missing embedded resource)");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
            };

            // The embedded JSON carries a JSONC attribution header (leading '//' lines);
            // the reader must skip comments or System.Text.Json throws a JsonException.
            using var doc = JsonDocument.Parse(
                stream,
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

            var entries = doc.RootElement.Deserialize<List<LeveEntry>>(options) ?? [];
            return new LeveDatabase(entries);
        }
        finally
        {
            stream?.Dispose();
        }
    }
}
