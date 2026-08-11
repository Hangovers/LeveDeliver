// Vendored from ECommons (https://github.com/NightmareXIV/ECommons) — MIT License,
// Copyright (c) 2023 NightmareXIV. See LeveDeliver/ECommonsVendored/VENDORED.md.
// Adapted to log through the vendored Services holder instead of Svc.

using ECommons;

namespace ECommons.Logging;

public static class PluginLog
{
    public static void Information(string s) => Services.Log.Information(s);
    public static void Error(string s) => Services.Log.Error(s);
    public static void Fatal(string s) => Services.Log.Fatal(s);
    public static void Debug(string s) => Services.Log.Debug(s);
    public static void Verbose(string s) => Services.Log.Verbose(s);
    public static void Warning(string s) => Services.Log.Warning(s);
    public static void LogInformation(string s) => Information(s);
    public static void LogError(string s) => Error(s);
    public static void LogFatal(string s) => Fatal(s);
    public static void LogDebug(string s) => Debug(s);
    public static void LogVerbose(string s) => Verbose(s);
    public static void LogWarning(string s) => Warning(s);
    public static void Log(string s) => Information(s);
}
