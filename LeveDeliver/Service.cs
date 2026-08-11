using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace LeveDeliver;

/// <summary>
/// Static service locator, matching the user's PluginSync pattern.
/// </summary>
public class Service
{
    public static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    public static ICommandManager Commands { get; set; } = null!;
    public static IFramework Framework { get; set; } = null!;
    public static IChatGui Chat { get; set; } = null!;
    public static IGameGui GameGui { get; set; } = null!;
    public static IDataManager Data { get; set; } = null!;
    public static IObjectTable Objects { get; set; } = null!;
    public static ITargetManager Targets { get; set; } = null!;
    public static ICondition Condition { get; set; } = null!;
    public static IClientState ClientState { get; set; } = null!;
    public static IPluginLog Log { get; set; } = null!;
}
