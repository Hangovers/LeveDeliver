using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Runtime.CompilerServices;

namespace LeveDeliver.Game;

/// <summary>
/// QuestManager-backed leve state reads (allowances, accepted leves, completion).
/// All game-touching code here is unsafe by nature (native struct pointers).
/// </summary>
public static unsafe class LeveState
{
    /// <summary>Leve allowances left.</summary>
    public static int NumAllowances => QuestManager.Instance()->NumLeveAllowances;

    /// <summary>True if the leve is currently accepted (present in the LeveQuests array).</summary>
    public static bool IsAccepted(uint leveId)
    {
        var quests = QuestManager.Instance()->LeveQuests;
        for (var i = 0; i < quests.Length; i++)
        {
            if (quests[i].LeveId == (ushort)leveId)
                return true;
        }
        return false;
    }

    /// <summary>True if the leve has been completed at least once (repeat-capped flag).</summary>
    public static bool IsComplete(uint leveId) => QuestManager.Instance()->IsLevequestComplete((ushort)leveId);

    /// <summary>Returns the active LeveWork entry for the leve, or null.</summary>
    public static LeveWork* GetLeveWork(uint leveId)
    {
        var quests = QuestManager.Instance()->LeveQuests;
        for (var i = 0; i < quests.Length; i++)
        {
            if (quests[i].LeveId == (ushort)leveId)
                return (LeveWork*)Unsafe.AsPointer(ref quests[i]);
        }
        return null;
    }

    /// <summary>LeveWork.Sequence == 255 means the leve is ready to be turned in.</summary>
    public static bool IsReadyToTurnIn(uint leveId)
    {
        var work = GetLeveWork(leveId);
        return work != null && work->Sequence == 255;
    }

    /// <summary>Ids of all currently accepted leves.</summary>
    public static IReadOnlyList<ushort> GetActiveLeveIds()
    {
        var ids = new List<ushort>();
        var quests = QuestManager.Instance()->LeveQuests;
        for (var i = 0; i < quests.Length; i++)
        {
            if (quests[i].LeveId != 0)
                ids.Add(quests[i].LeveId);
        }
        return ids;
    }

    /// <summary>Inventory count of the item, HQ and NQ combined (incl. collectable variants via +500000).</summary>
    public static int GetItemCount(uint itemId, bool includeHq = true)
    {
        var inv = InventoryManager.Instance();
        if (inv == null)
            return 0;
        var nq = inv->GetInventoryItemCount(itemId);
        var collectable = inv->GetInventoryItemCount(itemId + 500_000);
        if (!includeHq)
            return nq + collectable;
        return nq + inv->GetInventoryItemCount(itemId, true) + collectable;
    }
}
