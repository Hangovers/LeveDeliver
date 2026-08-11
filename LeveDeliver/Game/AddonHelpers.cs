using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace LeveDeliver.Game;

/// <summary>
/// Small native helpers: addon presence/visibility, node text, NPC interaction.
/// </summary>
public static unsafe class AddonHelpers
{
    public static AtkUnitBase* GetAddon(string name)
    {
        var ptr = Service.GameGui.GetAddonByName(name, 1);
        return (AtkUnitBase*)ptr.Address;
    }

    public static bool IsVisible(string name)
    {
        var addon = GetAddon(name);
        return addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool IsReady(string name)
    {
        var addon = GetAddon(name);
        return addon != null && addon->IsReady;
    }

    /// <summary>Reads the text of a node in the addon's UldManager NodeList by index.</summary>
    public static string GetNodeText(AtkUnitBase* addon, int[] nodeIndices)
    {
        if (addon == null)
            return string.Empty;

        var uld = &addon->UldManager;
        AtkResNode* node = null;
        for (var i = 0; i < nodeIndices.Length; i++)
        {
            var index = nodeIndices[i];
            if (index < 0 || index >= uld->NodeListCount)
                return string.Empty;
            node = uld->NodeList[index];
            if (node == null)
                return string.Empty;
            // More nodes to traverse
            if (i < nodeIndices.Length - 1)
            {
                if (node->Type != NodeType.Component)
                    return string.Empty;
                uld = &((AtkComponentNode*)node)->Component->UldManager;
            }
        }

        if (node->Type == NodeType.Counter)
            return ((AtkCounterNode*)node)->NodeText.ToString();
        if (node->Type == NodeType.Text)
            return ((AtkTextNode*)node)->NodeText.ToString();
        return string.Empty;
    }

    /// <summary>
    /// Interacts with the nearest game object with the given DataId (ENpcResidentId),
    /// exactly like ChilledLeves Utils.InteractWithObject: only if it is targetable.
    /// </summary>
    public static bool TryInteractWithDataId(uint dataId)
    {
        foreach (var obj in Service.Objects)
        {
#pragma warning disable CS0618 // DataId renamed to BaseId upstream; both refer to the ENpcResidentId
            if (obj.DataId != dataId)
                continue;
#pragma warning restore CS0618
            var go = (GameObject*)obj.Address;
            if (go == null || !go->GetIsTargetable())
                return false;
            if (go->GetObjectKind() == FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind.Pc)
                return false;
            TargetSystem.Instance()->InteractWithObject(go, false);
            return true;
        }
        return false;
    }

    /// <summary>Distance to the nearest object with the given DataId, or float.MaxValue.</summary>
    public static float DistanceToDataId(uint dataId)
    {
        var player = GetLocalPlayer();
        if (player == null)
            return float.MaxValue;
        foreach (var obj in Service.Objects)
        {
#pragma warning disable CS0618 // DataId renamed to BaseId upstream; both refer to the ENpcResidentId
            if (obj.DataId == dataId)
#pragma warning restore CS0618
                return Vector3.Distance(player.Position, obj.Position);
        }
        return float.MaxValue;
    }

    private static IGameObject? GetLocalPlayer()
    {
        foreach (var obj in Service.Objects)
        {
            if (obj.ObjectKind == DalamudObjectKind.Pc)
                return obj;
        }
        return null;
    }

    /// <summary>True if the player is busy with any animation/lock that would break dialog flow.</summary>
    public static bool PlayerIsBusy()
        => Service.Condition[ConditionFlag.BetweenAreas]
        || Service.Condition[ConditionFlag.BetweenAreas51]
        || Service.Condition[ConditionFlag.Jumping]
        || Service.Condition[ConditionFlag.Jumping61]
        || Service.Condition[ConditionFlag.Casting]
        || Service.Condition[ConditionFlag.Occupied]
        || Service.Condition[ConditionFlag.OccupiedInQuestEvent]
        || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]
        || Service.Condition[ConditionFlag.OccupiedSummoningBell]
        || Service.Condition[ConditionFlag.InCombat]
        || Service.Condition[ConditionFlag.Mounting]
        || Service.Condition[ConditionFlag.Mounted];
}
