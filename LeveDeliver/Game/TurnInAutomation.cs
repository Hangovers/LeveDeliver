using FFXIVClientStructs.FFXIV.Component.GUI;
using Callback = ECommons.Automation.Callback;

namespace LeveDeliver.Game;

/// <summary>
/// Built-in Request-window slot filler. This is the ContextIconMenu routine from
/// Pandora's Box "Auto-select Turn-ins" (AutoSelectTurnin.cs, BSD-3-Clause,
/// copyright PandorasBox contributors) — reimplemented locally so LeveDeliver
/// works standalone. Same values, same order: fire the slot callback, then
/// confirm the item via the ContextIconMenu callback.
/// </summary>
public static unsafe class TurnInAutomation
{
    private const uint ConfirmIconMenuValue = 1021003;

    /// <summary>
    /// Attempts to fill the next empty slot in the Request window.
    /// Returns true if a slot callback was fired (or the context menu was confirmed);
    /// false if nothing happened this tick.
    /// </summary>
    public static bool FillNextSlot(AddonRequest* request)
    {
        if (request == null)
            return false;

        var contextMenuPtr = Service.GameGui.GetAddonByName("ContextIconMenu", 1);
        var contextMenu = (AtkUnitBase*)contextMenuPtr.Address;
        if (contextMenu != null && contextMenu->IsVisible)
        {
            // The item picker is open for the last slot we clicked: confirm it.
            Callback.Fire(contextMenu, false, 0, 0, ConfirmIconMenuValue, 0, 0);
            return true;
        }

        for (var i = 0; i < request->EntryCount; i++)
        {
            if (IsSlotFilled(request, i))
                continue;
            Callback.Fire(&request->AtkUnitBase, false, 2, i, 0, 0);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a slot already has an item in it by inspecting the request
    /// window's slot components (nodes 3..7 and their sub-slots 9..13, per
    /// ECommons AddonMaster.Request.IsFilled).
    /// </summary>
    private static bool IsSlotFilled(AddonRequest* request, int slot)
    {
        var nodeId = 3u + (uint)slot;
        var node = request->GetComponentNodeById(nodeId);
        var sub = request->GetComponentNodeById(nodeId + 6);
        if (node == null || sub == null)
            return false;
        // When a slot is empty the game shows the "click to add item" placeholder;
        // when filled, the drag-drop node is visible instead.
        return !node->AtkResNode.IsVisible();
    }
}
