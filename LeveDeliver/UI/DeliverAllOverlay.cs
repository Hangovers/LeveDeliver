using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using LeveDeliver.Game;
using System.Numerics;

namespace LeveDeliver.UI;

/// <summary>
/// The "Deliver All" overlay button drawn NEXT TO the JournalDetail Initiate
/// (Accept) button while the leve list is open.
///
/// Deliberately does NOT hide the real Initiate button: hiding it every frame
/// and restoring it when conditions change causes visible flickering and a
/// transparent unclickable ghost window. Instead we leave the real button
/// alone and draw our own button immediately to its right (Pandora's Box
/// "Trade All Collectables" overlay technique, BSD-3-Clause, adapted).
/// </summary>
public unsafe class DeliverAllOverlay : IDisposable
{
    private const string WindowName = "###LeveDeliverOverlay";

    private readonly AddonFlow flow;
    private readonly Configuration config;

    public DeliverAllOverlay(AddonFlow flow, Configuration config)
    {
        this.flow = flow;
        this.config = config;
    }

    public void Dispose()
    {
    }

    /// <summary>Draws the overlay on the ImGui frame, if conditions are met.</summary>
    public void Draw()
    {
        if (!this.config.ShowOverlay)
            return;

        var journal = AddonHelpers.GetAddon("JournalDetail");
        if (journal == null || !journal->IsVisible || !journal->IsFullyLoaded())
            return;

        var detail = (AddonJournalDetail*)journal;
        var button = detail->InitiateButton;
        if (button == null || !button->AtkResNode->IsVisible())
            return;

        // Position our button to the RIGHT of the real Initiate button.
        var resNode = button->AtkResNode;
        var position = GetNodePosition(resNode);
        var scale = GetNodeScale(resNode);
        var size = new Vector2(resNode->Width, resNode->Height) * scale;
        position.X += size.X + 6f * scale.X;

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
            | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

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
