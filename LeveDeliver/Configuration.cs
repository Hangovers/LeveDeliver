using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace LeveDeliver;

public class Configuration : IPluginConfiguration, IDisposable
{
    public int Version { get; set; } = 0;

    // IMPORTANT: no constructor with parameters. Dalamud deserializes the plugin
    // config from JSON via Newtonsoft.Json, which will happily invoke a
    // parameterized ctor passing null for the parameters — causing a
    // NullReferenceException and a plugin load failure. Config classes must be
    // plain data; the plugin interface is supplied via Load/Save methods.
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public IDalamudPluginInterface? PluginInterface { get; set; }

    public bool ShowOverlay { get; set; } = true;
    public bool UsePandoraAutofill { get; set; } = true;
    public bool StopWhenItemsLow { get; set; } = true;
    public bool VerboseLogging { get; set; }

    public static Configuration Load(IDalamudPluginInterface pluginInterface)
    {
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.PluginInterface = pluginInterface;
        return config;
    }

    public void Save() => this.PluginInterface?.SavePluginConfig(this);

    public void Dispose() => this.Save();
}
