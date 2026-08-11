// Vendored from ECommons (https://github.com/NightmareXIV/ECommons) — MIT License,
// Copyright (c) 2023 NightmareXIV. See LeveDeliver/ECommonsVendored/VENDORED.md.
//
// Trimmed to only the plugin-presence detection used by LeveDeliver
// (TryGetDalamudPlugin with ignoreCache). The cache/monitor machinery from the
// original is omitted; the plugin manager is resolved via reflection on every
// call, which is what ChilledLeves' Utils.HasPlugin relies on anyway.

using Dalamud.Plugin;
using ECommons.Logging;
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Loader;

namespace ECommons.Reflection;
#pragma warning disable CS8600, CS8602, CS8603, CS8604, CS8625 // reflection-heavy code, same as upstream

public static class DalamudReflector
{
    public static bool TryGetDalamudPlugin(string internalName, out IDalamudPlugin instance, bool suppressErrors = false, bool ignoreCache = false)
    {
        try
        {
            var pluginManager = GetPluginManager();
            var installedPlugins = (IList)pluginManager.GetType().GetProperty("InstalledPlugins").GetValue(pluginManager);
            foreach (var t in installedPlugins)
            {
                if ((string)t.GetType().GetProperty("InternalName").GetValue(t) == internalName)
                {
                    var type = t.GetType().Name == "LocalDevPlugin" ? t.GetType().BaseType : t.GetType();
                    var plugin = (IDalamudPlugin)type.GetField("instance", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(t);
                    if (plugin == null)
                    {
                        PluginLog.Warning($"Found requested plugin {internalName} but it was null");
                    }
                    else
                    {
                        instance = plugin;
                        return true;
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (!suppressErrors)
                PluginLog.Error($"Can't find {internalName} plugin: " + e.Message);
        }
        instance = null;
        return false;
    }

    private static object GetPluginManager()
    {
        var pluginInterface = ECommons.Services.PluginInterface;
        return pluginInterface.GetType().Assembly
            .GetType("Dalamud.Service`1", true)
            .MakeGenericType(pluginInterface.GetType().Assembly.GetType("Dalamud.Plugin.Internal.PluginManager", true))
            .GetMethod("Get").Invoke(null, BindingFlags.Default, null, Array.Empty<object>(), null);
    }
}
#pragma warning restore CS8600, CS8602, CS8603, CS8604, CS8625
