using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using LeveDeliver.Game;
using LeveDeliver.IPC;

namespace LeveDeliver.UI;

/// <summary>Config + status window, registered on the plugin's WindowSystem.</summary>
public class MainWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly AddonFlow flow;
    private readonly PandoraIPC pandora;

    public MainWindow(Configuration config, AddonFlow flow, PandoraIPC pandora)
        : base("Leve Deliver###levedeliver-main")
    {
        this.config = config;
        this.flow = flow;
        this.pandora = pandora;
        this.Size = new Vector2(420, 260);
        this.SizeCondition = Dalamud.Bindings.ImGui.ImGuiCond.FirstUseEver;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var showOverlay = this.config.ShowOverlay;
        if (ImGui.Checkbox("Show 'Deliver All' overlay on the leve window", ref showOverlay))
        {
            this.config.ShowOverlay = showOverlay;
            this.config.Save();
        }

        var usePandora = this.config.UsePandoraAutofill;
        if (ImGui.Checkbox("Use Pandora auto-fill when available", ref usePandora))
        {
            this.config.UsePandoraAutofill = usePandora;
            this.config.Save();
        }

        var stopLow = this.config.StopWhenItemsLow;
        if (ImGui.Checkbox("Stop when inventory is below the required amount", ref stopLow))
        {
            this.config.StopWhenItemsLow = stopLow;
            this.config.Save();
        }

        var verbose = this.config.VerboseLogging;
        if (ImGui.Checkbox("Verbose logging", ref verbose))
        {
            this.config.VerboseLogging = verbose;
            this.config.Save();
        }

        ImGui.Separator();

        ImGui.TextUnformatted($"Pandora's Box: {(this.pandora.Installed ? "detected" : "not detected")}");
        if (this.pandora.Installed)
        {
            ImGui.TextUnformatted($"Auto-select Turn-ins: {(this.pandora.IsAutofillActive ? "active" : "inactive")}");
        }

        ImGui.Separator();

        ImGui.TextUnformatted($"Allowances left: {LeveState.NumAllowances}");
        if (this.flow.CurrentLeve != null)
            ImGui.TextUnformatted($"Current leve: {this.flow.CurrentLeve.Name} ({this.flow.CurrentLeve.Job} lv{this.flow.CurrentLeve.LevelRequired})");
        ImGui.TextUnformatted($"State: {this.flow.State}");
        ImGui.TextUnformatted($"Delivered: {this.flow.Deliveries}");
        if (!string.IsNullOrEmpty(this.flow.LastResult))
            ImGui.TextUnformatted($"Last result: {this.flow.LastResult}");

        ImGui.Separator();

        if (this.flow.Running)
        {
            if (ImGui.Button("Abort"))
                this.flow.Abort("aborted by user");
        }
        else if (ImGui.Button("Start (with leve selected in the leve window)"))
        {
            this.flow.StartFromGuildLeve();
        }
    }
}
