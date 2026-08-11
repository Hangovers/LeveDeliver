using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace LeveDeliver.IPC;

/// <summary>
/// IPC client for Pandora's Box, matching the exact provider names registered in
/// PandorasBox/IPC/PandoraIPC.cs: GetFeatureEnabled, GetConfigEnabled, PauseFeature.
/// Plugin presence is detected via reflection (ECommons DalamudReflector), same
/// as ChilledLeves Utils.HasPlugin.
/// </summary>
public class PandoraIPC : IDisposable
{
    public const string PluginName = "PandorasBox";
    public const string AutofillFeature = "Auto-select Turn-ins";
    public const string AutofillConfigProp = "AutoSelect";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IChatGui chat;

    private ICallGateSubscriber<string, bool?>? featureSub;
    private ICallGateSubscriber<string, string, bool?>? configSub;
    private ICallGateSubscriber<string, int, object?>? pauseSub;

    public PandoraIPC(IDalamudPluginInterface pluginInterface, IChatGui chat)
    {
        this.pluginInterface = pluginInterface;
        this.chat = chat;

        this.featureSub = this.pluginInterface.GetIpcSubscriber<string, bool?>("PandorasBox.GetFeatureEnabled");
        this.configSub = this.pluginInterface.GetIpcSubscriber<string, string, bool?>("PandorasBox.GetConfigEnabled");
        this.pauseSub = this.pluginInterface.GetIpcSubscriber<string, int, object?>("PandorasBox.PauseFeature");
    }

    public void Dispose()
    {
        // IpcSubscribers have no unregister for consumers; just drop references.
        this.featureSub = null;
        this.configSub = null;
        this.pauseSub = null;
    }

    /// <summary>True if Pandora's Box is installed and loaded (reflection-based).</summary>
    public bool Installed { get; private set; }

    /// <summary>True if the "Auto-select Turn-ins" feature is enabled and its AutoSelect config is on.</summary>
    public bool IsAutofillActive
        => this.Installed
           && this.featureSub!.InvokeFunc(AutofillFeature) == true
           && this.configSub!.InvokeFunc(AutofillFeature, AutofillConfigProp) == true;

    /// <summary>True if Pandora's auto-fill is enabled but only via the feature toggle.</summary>
    public bool IsAutofillFeatureEnabled
        => this.Installed && this.featureSub!.InvokeFunc(AutofillFeature) == true;

    /// <summary>Checks plugin presence via reflection. Call once after plugin load.</summary>
    public void CheckInstalled()
    {
        this.Installed = ECommons.Reflection.DalamudReflector.TryGetDalamudPlugin(PluginName, out _, false, true);
        if (this.Installed)
            Service.Log.Information($"Pandora's Box detected — IPC to '{AutofillFeature}' available.");
    }

    /// <summary>
    /// Pauses Pandora's auto-fill for the given duration (ms) so our own filler
    /// does not fight it. No-op if the feature is not active.
    /// </summary>
    public void PauseAutofill(int ms)
    {
        if (!this.Installed)
            return;
        try
        {
            this.pauseSub!.InvokeAction(AutofillFeature, ms);
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "Failed to pause Pandora auto-fill");
        }
    }

    public void ChatStatus()
    {
        if (this.Installed)
        {
            if (this.IsAutofillActive)
                this.chat.Print($"LeveDeliver: Pandora '{AutofillFeature}' active — using it for slot filling.");
            else
                this.chat.Print($"LeveDeliver: Pandora installed but '{AutofillFeature}' is off — using built-in filler.");
        }
        else
        {
            this.chat.Print("LeveDeliver: Pandora's Box not detected — using built-in slot filler.");
        }
    }
}
