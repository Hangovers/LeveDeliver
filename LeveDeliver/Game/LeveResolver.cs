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

        if (db.ByName.TryGetValue(name, out var byName))
            return byName;

        // Fallback: try to resolve the name to a Leve row via Lumina, then match by RowId.
        foreach (var row in Service.Data.GetExcelSheet<Lumina.Excel.Sheets.Leve>())
        {
            if (row.Name.ToString() == name)
                return db.ById.GetValueOrDefault(row.RowId);
        }

        return null;
    }
}
