using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace LeveDeliver;

public class Configuration : IPluginConfiguration, IDisposable
{
    public int Version { get; set; } = 0;

    private readonly IDalamudPluginInterface pluginInterface;

    public bool ShowOverlay { get; set; } = true;
    public bool UsePandoraAutofill { get; set; } = true;
    public bool StopWhenItemsLow { get; set; } = true;
    public bool VerboseLogging { get; set; }

    public Configuration(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        this.pluginInterface.SavePluginConfig(this);
    }

    public static Configuration Load(IDalamudPluginInterface pluginInterface)
        => pluginInterface.GetPluginConfig() as Configuration ?? new Configuration(pluginInterface);

    public void Save() => this.pluginInterface.SavePluginConfig(this);

    public void Dispose() => this.Save();
}
