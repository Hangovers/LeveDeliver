# Vendored ECommons sources

The ECommons submodule is not initialized in this checkout (no network for
`git submodule update`), so the minimal pieces of ECommons used by LeveDeliver
are vendored here from https://github.com/NightmareXIV/ECommons (MIT license,
Copyright (c) 2023 NightmareXIV).

Vendored files (all carry their original MIT license header):

- `Callback.cs` — `ECommons.Automation.Callback.Fire` (fires addon callbacks)
- `EzThrottler.cs` + `EzThrottler{T}.cs` — `ECommons.Throttlers`
- `PluginLog.cs` — `ECommons.Logging` (thin wrapper over Dalamud IPluginLog)
- `AddonMasterImplementations/!AddonMasterBase.cs` — AddonMaster base class
- `AddonMasterImplementations/Request.cs` — Request window (HandOver / IsFilled)
- `AddonMasterImplementations/JournalDetail.cs` — leve accept card
- `AddonMasterImplementations/SelectIconString.cs` — turn-in picker
- `AddonMasterImplementations/SelectString.cs` — levemete menu
- `Reflection/DalamudReflector.cs` — plugin-presence detection (trimmed: only
  TryGetDalamudPlugin(string, out IDalamudPlugin, bool, bool))

When the submodule is initialized, `LeveDeliver.csproj` prefers the real
`..\ECommons\ECommons\ECommons.csproj` ProjectReference and this folder is not
compiled. To switch back to vendored mode, delete/rename `ECommons/` and add
`<Compile Include="ECommonsVendored/**/*.cs" />` to the csproj.

The embedded `LeveDatabase.json` data is from xivganon/LEDE (MIT) — see its header.
