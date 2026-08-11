using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LeveDeliver.Data;
using LeveDeliver.IPC;
using LeveDeliver.UI;
using Callback = ECommons.Automation.Callback;
namespace LeveDeliver.Game;

public enum LeveFlowState
{
    Idle,
    Accepting,        // fired JournalDetail accept, waiting for LeveQuests entry
    ClosingMenu,      // closing GuildLeve / stepping back through menus
    WalkingDelivery,  // waiting for / driving interaction with the delivery NPC
    SelectingTurnin,  // SelectIconString picker
    HandingOver,      // Request window, fill + hand over
    ConfirmingTurnin, // SelectYesno / JournalResult
    Reopening,        // walking back to the levemete to restart
    Done,
}

/// <summary>
/// The core accept -> deliver -> repeat state machine, driven from IFramework.Update.
/// All callbacks are the VERIFIED values from the reference repos (ChilledLeves /
/// PandorasBox) — do not invent new ones.
/// </summary>
public unsafe class AddonFlow : IDisposable
{
    private const int ThrottleMs = 100;      // min time between callback fires
    private const int AddonTimeoutMs = 15_000; // give up if an addon never appears
    private const float InteractRange = 7f;

    private readonly LeveDatabase db;
    private readonly PandoraIPC pandora;
    private readonly Configuration config;
    private readonly IUiBuilder uiBuilder;
    private long lastActionMs;

    private LeveEntry? leve;
    private LeveFlowState state;
    private long stateEnteredMs;
    private int deliveries;
    private bool abortRequested;
    private int lastAllowances;

    public LeveFlowState State => this.state;
    public bool Running => this.state != LeveFlowState.Idle && this.state != LeveFlowState.Done;
    public LeveEntry? CurrentLeve => this.leve;
    public int Deliveries => this.deliveries;
    public string LastResult { get; private set; } = "";

    public event Action? StateChanged;

    public AddonFlow(LeveDatabase db, PandoraIPC pandora, Configuration config, IUiBuilder uiBuilder)
    {
        this.db = db;
        this.pandora = pandora;
        this.config = config;
        this.uiBuilder = uiBuilder;
    }

    public void Dispose()
    {
        // nothing to dispose; Framework hook lives in Plugin
    }

    public void Tick()
    {
        if (!this.Running)
            return;

        try
        {
            this.TickCore();
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "LeveDeliver flow crashed");
            this.Abort("internal error: " + ex.Message);
        }
    }

    private void TickCore()
    {
        switch (this.state)
        {
            case LeveFlowState.Accepting:
                this.TickAccepting();
                break;
            case LeveFlowState.ClosingMenu:
                this.TickClosingMenu();
                break;
            case LeveFlowState.WalkingDelivery:
                this.TickWalkingDelivery();
                break;
            case LeveFlowState.SelectingTurnin:
                this.TickSelectingTurnin();
                break;
            case LeveFlowState.HandingOver:
                this.TickHandingOver();
                break;
            case LeveFlowState.ConfirmingTurnin:
                this.TickConfirmingTurnin();
                break;
            case LeveFlowState.Reopening:
                this.TickReopening();
                break;
            case LeveFlowState.Done:
                this.TickDone();
                break;
        }
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Starts the loop for the leve currently selected in the GuildLeve window.</summary>
    public bool StartFromGuildLeve()
    {
        if (this.Running)
            return false;

        var entry = LeveResolver.ResolveSelected(this.db);
        if (entry == null)
        {
            this.LastResult = "No leve selected / leve not in database";
            Service.Chat.PrintError($"LeveDeliver: {this.LastResult}.");
            return false;
        }

        if (LeveState.NumAllowances <= 0)
        {
            this.LastResult = "No leve allowances left";
            Service.Chat.PrintError("LeveDeliver: no leve allowances left.");
            return false;
        }

        if (this.config.StopWhenItemsLow && LeveState.GetItemCount(entry.ItemId) < entry.Quantity)
        {
            this.LastResult = $"Not enough {entry.ItemName} (have < {entry.Quantity})";
            Service.Chat.PrintError($"LeveDeliver: not enough {entry.ItemName} in inventory (need {entry.Quantity}).");
            return false;
        }

        this.leve = entry;
        this.deliveries = 0;
        this.abortRequested = false;
        this.lastAllowances = LeveState.NumAllowances;
        this.Verbose($"Starting delivery loop for leve {entry.Id} \"{entry.Name}\" ({entry.Job} lv{entry.LevelRequired}).");
        this.SetState(LeveFlowState.Accepting);
        return true;
    }

    /// <summary>Aborts the loop cleanly and resets to Idle, leaving the game UI as-is.</summary>
    public void Abort(string? reason = null)
    {
        if (!this.Running && this.state != LeveFlowState.Done)
            return;

        this.LastResult = reason ?? "Aborted by user";
        if (!string.IsNullOrEmpty(reason))
            Service.Chat.PrintError($"LeveDeliver: {reason}");
        this.Verbose($"Flow aborted: {this.LastResult}");
        this.SetState(LeveFlowState.Idle);
    }

    // ---------------------------------------------------------------- internals

    private void SetState(LeveFlowState newState)
    {
        this.state = newState;
        this.stateEnteredMs = Environment.TickCount64;
        this.Verbose($"-> {newState}");
        this.StateChanged?.Invoke();
    }

    private bool Throttle()
    {
        var now = Environment.TickCount64;
        if (now - this.lastActionMs < ThrottleMs)
            return false;
        this.lastActionMs = now;
        return true;
    }

    private bool TimedOut()
        => Environment.TickCount64 - this.stateEnteredMs > AddonTimeoutMs;

    private void Verbose(string message)
    {
        if (this.config.VerboseLogging)
            Service.Log.Debug(message);
    }

    private void FinishLoop(string reason)
    {
        this.LastResult = reason;
        Service.Chat.Print(
            $"LeveDeliver: delivered {this.deliveries} time(s) for \"{this.leve?.Name}\" ({LeveState.NumAllowances} allowance(s) left). {reason}");
        this.Verbose($"Loop finished: {reason}");
        this.SetState(LeveFlowState.Done);
    }

    // ---------------------------------------------------------------- accept phase

    private void TickAccepting()
    {
        if (this.abortRequested)
        {
            this.Abort("aborted by user");
            return;
        }

        if (LeveState.IsAccepted(this.leve!.Id))
        {
            this.Verbose("Leve accepted, closing menu.");
            this.SetState(LeveFlowState.ClosingMenu);
            return;
        }

        if (!AddonHelpers.IsVisible("GuildLeve"))
        {
            if (this.TimedOut())
            {
                this.Abort("GuildLeve window closed unexpectedly");
                return;
            }
            return;
        }

        // A freshly-reopened GuildLeve has nothing selected: the JournalDetail card
        // only appears AFTER a leve is selected. Select the card for this leve first
        // (ChilledLeves GuildLeve.cs: AtkValues[25] = entry count, AtkValues[626 + i*2]
        // = entry name, select via callback (13, index, leveId)).
        if (!AddonHelpers.IsVisible("JournalDetail"))
        {
            if (this.TimedOut())
            {
                this.Abort("GuildLeve window closed unexpectedly");
                return;
            }

            if (!this.Throttle())
                return;

            var guildLeve = (AddonGuildLeve*)AddonHelpers.GetAddon("GuildLeve");
            if (guildLeve == null)
                return;

            var numEntries = guildLeve->AtkValues[25].UInt;
            for (var i = 0; i < numEntries; i++)
            {
                var nameValue = guildLeve->AtkValues[626 + i * 2];
                if (nameValue.String.Value == null)
                    continue;
                var name = Dalamud.Memory.MemoryHelper.ReadSeStringNullTerminated((nint)nameValue.String.Value).ToString();
                if (string.Equals(name, this.leve!.Name, StringComparison.OrdinalIgnoreCase))
                {
                    this.Verbose($"Selecting leve card {i} in GuildLeve.");
                    Callback.Fire(&guildLeve->AtkUnitBase, true, 13, i, (int)this.leve.Id);
                    return;
                }

                this.Verbose($"Leve card {i} is \"{name}\" — not a match, continuing.");
            }

            // Leve not in the list: give the accept path a grace period before failing.
            return;
        }

        if (this.Throttle())
        {
            this.Verbose("Firing JournalDetail accept.");
            Callback.Fire(AddonHelpers.GetAddon("JournalDetail"), true, 3, (int)this.leve!.Id);
        }
    }

    private void TickClosingMenu()
    {
        // Close the leve list if it is still open, and skip Talk dialogs.
        if (AddonHelpers.IsVisible("GuildLeve"))
        {
            if (this.Throttle())
                Callback.Fire(AddonHelpers.GetAddon("GuildLeve"), true, -1);
            return;
        }

        if (AddonHelpers.IsVisible("Talk"))
        {
            if (this.Throttle())
                Callback.Fire(AddonHelpers.GetAddon("Talk"), true);
            return;
        }

        if (AddonHelpers.IsVisible("SelectString"))
        {
            if (this.Throttle())
            {
                // Leave is always the LAST entry of a levemete's SelectString menu
                // (ChilledLeves uses vendor-specific leave indices, typically 4).
                // Firing index 0 would click the first menu item instead of leaving.
                var selectString = (AddonSelectString*)AddonHelpers.GetAddon("SelectString");
                if (selectString != null)
                {
                    var entryCount = selectString->PopupMenu.PopupMenu.EntryCount;
                    if (entryCount > 0)
                        Callback.Fire(AddonHelpers.GetAddon("SelectString"), true, entryCount - 1);
                }
            }
            return;
        }

        if (this.TimedOut())
        {
            this.Abort("menu did not close in time");
            return;
        }

        this.SetState(LeveFlowState.WalkingDelivery);
    }

    private void TickWalkingDelivery()
    {
        var npcId = this.leve!.TargetID;

        // If the delivery window is already up, proceed.
        if (AddonHelpers.IsVisible("SelectIconString")
            || AddonHelpers.IsVisible("Request")
            || AddonHelpers.IsVisible("SelectYesno"))
        {
            this.SetState(LeveFlowState.SelectingTurnin);
            return;
        }

        // The user can talk to the NPC themselves; we also auto-interact when in range.
        if (AddonHelpers.IsVisible("Talk") || AddonHelpers.IsVisible("SelectString"))
        {
            if (this.Throttle())
            {
                if (AddonHelpers.IsVisible("Talk"))
                    Callback.Fire(AddonHelpers.GetAddon("Talk"), true);
                else
                    Callback.Fire(AddonHelpers.GetAddon("SelectString"), true, 0);
            }
            return;
        }

        if (this.Throttle())
        {
            if (AddonHelpers.DistanceToDataId(npcId) <= InteractRange)
            {
                if (!AddonHelpers.TryInteractWithDataId(npcId))
                    return; // targetable check failed; retry next tick
                this.Verbose("Interacting with delivery NPC.");
            }
            else
            {
                // No NPC nearby: wait and prompt (no navigation).
                Service.Chat.Print(
                    $"LeveDeliver: talking to {this.leve!.TargetName} (delivery NPC). Interact with the NPC to continue...");
            }
        }

        if (this.TimedOut())
            this.Abort("delivery NPC interaction timed out");
    }

    private void TickSelectingTurnin()
    {
        if (this.abortRequested)
        {
            this.Abort("aborted by user");
            return;
        }

        if (LeveState.IsAccepted(this.leve!.Id) == false)
        {
            this.Verbose("Leve no longer accepted — turn-in done.");
            this.deliveries++;
            this.SetState(LeveFlowState.Reopening);
            return;
        }

        if (AddonHelpers.IsVisible("SelectIconString"))
        {
            if (!this.Throttle())
                return;

            // Find the entry matching the leve name (ChilledLeves GetCallback: nodes 2/i/4, callback i-1).
            var addon = AddonHelpers.GetAddon("SelectIconString");
            for (var i = 1; i < 18; i++)
            {
                var text = AddonHelpers.GetNodeText(addon, [2, i, 4]);
                if (string.Equals(text, this.leve!.Name, StringComparison.OrdinalIgnoreCase))
                {
                    this.Verbose($"Selecting turn-in entry {i - 1}.");
                    Callback.Fire(addon, true, i - 1);
                    return;
                }
            }

            this.Abort($"leve \"{this.leve!.Name}\" not found in turn-in list");
            return;
        }

        if (AddonHelpers.IsVisible("Request"))
        {
            this.SetState(LeveFlowState.HandingOver);
            return;
        }

        if (AddonHelpers.IsVisible("SelectYesno"))
        {
            this.SetState(LeveFlowState.ConfirmingTurnin);
            return;
        }

        if (AddonHelpers.IsVisible("Talk"))
        {
            if (this.Throttle())
                Callback.Fire(AddonHelpers.GetAddon("Talk"), true);
            return;
        }

        if (this.TimedOut())
            this.Abort("turn-in list did not open");
    }

    private void TickHandingOver()
    {
        if (this.abortRequested)
        {
            this.Abort("aborted by user");
            return;
        }

        var request = (AddonRequest*)AddonHelpers.GetAddon("Request");
        if (request == null || !request->AtkUnitBase.IsVisible)
        {
            if (this.TimedOut())
                this.Abort("Request window disappeared");
            return;
        }

        var filled = request->HandOverButton != null && request->HandOverButton->IsEnabled;
        var usePandora = this.config.UsePandoraAutofill && this.pandora.IsAutofillActive;

        if (filled)
        {
            if (this.Throttle())
            {
                // Click the HandOver button directly (same thing AddonMaster.Request.HandOver()
                // does) instead of firing an unverified callback.
                var button = request->HandOverButton;
                if (button != null && button->IsEnabled)
                {
                    this.Verbose("Handing over.");
                    var owner = button->AtkComponentBase.OwnerNode;
                    if (owner != null)
                    {
                        var evt = (FFXIVClientStructs.FFXIV.Component.GUI.AtkEvent*)owner->AtkResNode.AtkEventManager.Event;
                        if (evt != null)
                            request->AtkUnitBase.AtkEventListener.ReceiveEvent(evt->State.EventType, (int)evt->Param, evt);
                    }
                }
            }
            return;
        }

        if (usePandora)
        {
            // Pandora is filling the slots; just wait.
            return;
        }

        if (!this.Throttle())
            return;

        // Built-in filler (Pandora AutoSelectTurnin routine: BSD-3, attributed in TurnInAutomation).
        var filledAny = TurnInAutomation.FillNextSlot(request);
        if (!filledAny)
        {
            if (this.TimedOut())
                this.Abort("could not fill request slots");
        }
    }

    private void TickConfirmingTurnin()
    {
        if (this.abortRequested)
        {
            this.Abort("aborted by user");
            return;
        }

        if (AddonHelpers.IsVisible("SelectYesno"))
        {
            if (this.Throttle())
                Callback.Fire(AddonHelpers.GetAddon("SelectYesno"), true, 0);
            return;
        }

        if (AddonHelpers.IsVisible("JournalResult"))
        {
            if (this.Throttle())
                Callback.Fire(AddonHelpers.GetAddon("JournalResult"), true, 0, 0);
            return;
        }

        if (AddonHelpers.IsVisible("Talk"))
        {
            if (this.Throttle())
                Callback.Fire(AddonHelpers.GetAddon("Talk"), true);
            return;
        }

        if (this.TimedOut())
            this.Abort("turn-in confirmation timed out");
    }

    private void TickReopening()
    {
        if (this.abortRequested)
        {
            this.Abort("aborted by user");
            return;
        }

        // Check loop conditions.
        if (LeveState.NumAllowances <= 0)
        {
            this.FinishLoop("No leve allowances left.");
            return;
        }

        var itemCount = LeveState.GetItemCount(this.leve!.ItemId);
        if (this.config.StopWhenItemsLow && itemCount < this.leve.Quantity)
        {
            this.FinishLoop($"Not enough {this.leve.ItemName} in inventory.");
            return;
        }

        if (LeveState.IsAccepted(this.leve.Id))
        {
            this.Verbose("Leve still accepted — waiting for turn-in to register.");
            return;
        }

        if (AddonHelpers.IsVisible("GuildLeve"))
        {
            this.SetState(LeveFlowState.Accepting);
            return;
        }

        if (AddonHelpers.IsVisible("Talk"))
        {
            if (this.Throttle())
                Callback.Fire(AddonHelpers.GetAddon("Talk"), true);
            return;
        }

        if (AddonHelpers.IsVisible("SelectString"))
        {
            if (this.Throttle())
                Callback.Fire(AddonHelpers.GetAddon("SelectString"), true, 0);
            return;
        }

        // Interact with the levemete to reopen the menu.
        var levemeteId = this.leve.LevemeteID;
        if (this.Throttle())
        {
            if (AddonHelpers.DistanceToDataId(levemeteId) <= InteractRange)
            {
                if (AddonHelpers.TryInteractWithDataId(levemeteId))
                    this.Verbose("Interacting with levemete.");
            }
            else
            {
                Service.Chat.Print(
                    $"LeveDeliver: talk to {this.leve.LevemeteName} (levemete) to continue...");
            }
        }

        if (this.TimedOut())
            this.Abort("levemete interaction timed out");
    }

    private void TickDone()
    {
        // One-shot completion report; then go Idle.
        this.SetState(LeveFlowState.Idle);
    }
}
