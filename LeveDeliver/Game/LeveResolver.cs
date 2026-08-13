using Dalamud.Memory;
using LeveDeliver.Data;

namespace LeveDeliver.Game;

/// <summary>
/// Resolves the currently selected leve in the GuildLeve window to a RowId,
/// and maps leve RowIds to delivery NPCs via the embedded database.
/// </summary>
public static unsafe class LeveResolver
{
    /// <summary>
    /// Reads the leve name selected in the GuildLeve window, resolves it against the
    /// embedded database (by name, then by exact RowId). Returns null if unresolvable.
    /// </summary>
    public static LeveEntry? ResolveSelected(LeveDatabase db)
    {
        var addonPtr = Service.GameGui.GetAddonByName("GuildLeve");
        if (addonPtr == nint.Zero)
            return null;

        var guildLeve = (AddonGuildLeve*)addonPtr.Address;
        var numEntries = guildLeve->AtkValues[25].UInt;
        if (numEntries == 0)
            return null;

        // Name of the currently selected entry (AtkValue index 1233 per ChilledLeves GuildLeve.cs).
        var selected = guildLeve->AtkValues[1233].String;
        if (selected.Value == null)
            return null;

        var name = MemoryHelper.ReadSeStringNullTerminated((nint)selected.Value).ToString();
        if (string.IsNullOrEmpty(name))
            return null;

        // Normalize whitespace around the name (the client may pad it).
        var normalized = name.Trim();

        if (db.ByName.TryGetValue(normalized, out var byName))
            return byName;

        // Fallback 1: name contains decorations like "(Lv. 98)" — match on the
        // prefix before the first '('.
        var paren = normalized.IndexOf('(');
        if (paren > 0)
        {
            var shortName = normalized[..paren].Trim();
            if (db.ByName.TryGetValue(shortName, out byName))
                return byName;
        }

        // Fallback 2: localised client name — resolve via Lumina's Leve sheet
        // (the game localises the display name; the DB stores the English one).
        foreach (var row in Service.Data.GetExcelSheet<Lumina.Excel.Sheets.Leve>())
        {
            if (string.Equals(row.Name.ToString(), normalized, StringComparison.OrdinalIgnoreCase)
                || (paren > 0 && string.Equals(row.Name.ToString(), normalized[..paren].Trim(), StringComparison.OrdinalIgnoreCase)))
                return db.ById.GetValueOrDefault(row.RowId);
        }

        return null;
    }
}
