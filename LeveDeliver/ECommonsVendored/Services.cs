using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ECommons;

/// <summary>
/// Minimal static service holder for the vendored ECommons pieces
/// (see LeveDeliver/ECommonsVendored/VENDORED.md). Mirrors the subset of
/// ECommons.DalamudServices.Svc that the vendored files need.
/// </summary>
public static class Services
{
    public static IDalamudPluginInterface PluginInterface { get; internal set; } = null!;
    public static IFramework Framework { get; internal set; } = null!;
    public static IPluginLog Log { get; internal set; } = null!;
}
