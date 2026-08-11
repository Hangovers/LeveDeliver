# LeveDeliver

One-click repeat levequest delivery for FFXIV (Dalamud plugin, API 15 / .NET 10).

Automates the classic "farm easy gil" loop: accept a leve, turn it in, repeat until leve allowances or inventory run out — with one click. While the `GuildLeve` window is open and a leve is selected, a **"Deliver All"** button appears over the accept button. Click it and the plugin:

1. Accepts the currently-selected leve.
2. Closes the levequest menu and walks the delivery flow at the turn-in NPC.
3. Loops accept → deliver until **no leve allowances left**, **not enough items in inventory** for another turn-in, or the leve's repeat cap is reached.
4. Stops cleanly and reports in chat how many times it delivered.

No navigation: you stand at the levemete and the delivery NPC — the plugin only drives the UI dialogs. If an NPC is out of range it waits with a clear chat prompt.

## Commands

| Command | Action |
|---|---|
| `/levedeliver` | Toggle the main/config window |
| `/levedeliver start` | Start the loop for the leve selected in the open leve window |

## Pandora's Box (optional, soft dependency)

If [Pandora's Box](https://github.com/Pandorax/PandorasBox) is installed and its **"Auto-select Turn-ins"** feature is enabled, LeveDeliver detects it over IPC (`PandorasBox.GetFeatureEnabled` / `GetConfigEnabled`) and lets Pandora fill the handover slots, only clicking **Hand Over** itself.

If Pandora is absent or the feature is off, LeveDeliver falls back to its **built-in slot filler** — the same ContextIconMenu routine Pandora uses (see `Game/TurnInAutomation.cs`), so the plugin works fully standalone. The config window has a toggle for this behavior.

## Data attribution

- **Leve data** (leve row → item/quantity/job/level/levemete/delivery NPC) is derived from [xivganon/LEDE](https://github.com/xivganon/LEDE) `LeveDelivery/DB.lua`, **MIT licensed** — see the header of `LeveDeliver/Data/LeveDatabase.json`. Covers crafting (CRP/BSM/ARM/GSM/LTW/WVR/ALC/CUL) and fishing (FSH) leves; gathering (MIN/BTN) leves are not present in the upstream source.
- The levemete→leve mapping concept is additionally corroborated by LeveHelper (AGPL-3.0) `Data.cs` (data values only, no code).
- The overlay ("Trade All" pattern) and the slot-filling routine are reimplementations of Pandora's Box techniques (BSD-3-Clause, attributed in the source headers).
- No code was copied from GPL (ChilledLeves) or AGPL (LeveHelper) projects — only verified callback values and data facts.

## Install

**Dev plugin:** clone into your dev plugin folder with the `ECommons` submodule and build:

```bash
git clone --recurse-submodules https://github.com/hangovers/LeveDeliver
export PATH=$HOME/.dotnet:$PATH
export DALAMUD_HOME=$HOME/.xlcore/dalamud/Hooks/dev/
dotnet build LeveDeliver.sln
```

**Custom repo:** a release zip of `LeveDeliver.dll` + `LeveDeliver.json` + `icon.png` can be hosted anywhere and added via Dalamud's custom repo installer.

## Building on Linux

The csproj mirrors the PluginSync/ChilledLeves convention: on Linux, `DalamudLibPath` resolves from `DALAMUD_HOME` (defaulting to `~/.xlcore/dalamud/Hooks/dev/`), and on Windows it uses the standard `%APPDATA%\XIVLauncher\addon\Hooks\dev\`.

## License

MIT — see `LICENSE`. The embedded data file carries its own MIT attribution header.
