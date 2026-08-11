using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using LeveDeliver.Game;
using System.Numerics;

namespace LeveDeliver.UI;

/// <summary>
/// The "Deliver All" overlay button drawn over the JournalDetail Initiate button.
///
/// Technique from Pandora's Box "Trade All Collectables" (TradeAllCollectibles.cs,
/// BSD-3-Clause): on framework tick, locate the target button node, hide it, and
/// draw a transparent ImGui window of the same size at the same screen position
/// with our own button. Restores the original button when disabled.
/// </summary>
public unsafe class DeliverAllOverlay : IDisposable
{
    private const string WindowName = "###LeveDeliverOverlay";

    private readonly AddonFlow flow;
    private readonly Configuration config;
    private bool originalVisible;

    public DeliverAllOverlay(AddonFlow flow, Configuration config)
    {
        this.flow = flow;
        this.config = config;
    }

    public void Dispose()
    {
        this.RestoreButton();
    }

    /// <summary>Draws the overlay on the ImGui frame, if conditions are met.</summary>
    public void Draw()
    {
        if (!this.config.ShowOverlay)
        {
            this.RestoreButton();
            return;
        }

        var journal = AddonHelpers.GetAddon("JournalDetail");
        if (journal == null || !journal->IsVisible || !journal->IsFullyLoaded())
        {
            this.RestoreButton();
            return;
        }

        if (AddonHelpers.GetAddon("GuildLeve") == null)
        {
            this.RestoreButton();
            return;
        }

        // The Accept (Initiate) button of the JournalDetail addon.
        var detail = (AddonJournalDetail*)journal;
        var button = detail->InitiateButton;
        if (button == null || !button->AtkResNode->IsVisible())
        {
            this.RestoreButton();
            return;
        }

        // Hide the real button and draw our overlay over it (Trade All pattern).
        if (button->AtkResNode->IsVisible())
        {
            button->AtkResNode->ToggleVisibility(false);
            this.originalVisible = true;
        }

        var resNode = button->AtkResNode;
        var position = GetNodePosition(resNode);
        var scale = GetNodeScale(resNode);
        var size = new Vector2(resNode->Width, resNode->Height) * scale;

        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(position);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        var oldScale = ImGui.GetFont().Scale;
        ImGui.GetFont().Scale *= scale.X;
        ImGui.PushFont(ImGui.GetFont());
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, size);

        ImGui.Begin(
            WindowName,
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNavFocus
            | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

        var label = this.flow.Running ? "Delivering… Click to abort." : "Deliver All";
        if (ImGui.Button($"{label}###LeveDeliverStart", size))
        {
            if (this.flow.Running)
                this.flow.Abort("aborted by user");
            else
                this.flow.StartFromGuildLeve();
        }

        ImGui.End();
        ImGui.PopStyleVar(5);
        ImGui.GetFont().Scale = oldScale;
        ImGui.PopFont();
        ImGui.PopStyleColor();
    }

    /// <summary>Restores the original Initiate button visibility.</summary>
    public void RestoreButton()
    {
        var journal = AddonHelpers.GetAddon("JournalDetail");
        if (journal == null || !journal->IsFullyLoaded())
            return;

        var detail = (AddonJournalDetail*)journal;
        var button = detail->InitiateButton;
        if (button == null)
            return;

        if (this.originalVisible && !button->AtkResNode->IsVisible())
        {
            button->AtkResNode->ToggleVisibility(true);
        }
        this.originalVisible = false;
    }

    // Node position/scale helpers, from Pandora's Box Helpers/AtkResNodeHelper.cs
    // (BSD-3-Clause) — walks the parent chain accumulating offsets and scales.
    private static Vector2 GetNodePosition(AtkResNode* node)
    {
        var pos = new Vector2(node->X, node->Y);
        var par = node->ParentNode;
        while (par != null)
        {
            pos *= new Vector2(par->ScaleX, par->ScaleY);
            pos += new Vector2(par->X, par->Y);
            par = par->ParentNode;
        }
        return pos;
    }

    private static Vector2 GetNodeScale(AtkResNode* node)
    {
        var scale = new Vector2(node->ScaleX, node->ScaleY);
        while (node->ParentNode != null)
        {
            node = node->ParentNode;
            scale *= new Vector2(node->ScaleX, node->ScaleY);
        }
        return scale;
    }
}
