// Vendored from ECommons (https://github.com/NightmareXIV/ECommons) — MIT License, Copyright (c) 2023 NightmareXIV
// See LeveDeliver/ECommonsVendored/VENDORED.md.

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using AtkEvent = FFXIVClientStructs.FFXIV.Component.GUI.AtkEvent;

namespace ECommons.UIHelpers.AddonMasterImplementations;
public abstract unsafe class AddonMasterBase<T> : IAddonMasterBase where T : unmanaged
{
    protected AddonMasterBase(nint addon)
    {
        Addon = (T*)addon;
    }
    protected AddonMasterBase(void* addon)
    {
        Addon = (T*)addon;
    }

    /// <summary>
    /// User-friendly description, for use in plugin settings, etc.
    /// </summary>
    public abstract string AddonDescription { get; }
    public T* Addon { get; }
    public AtkUnitBase* Base => (AtkUnitBase*)Addon;
    public bool IsVisible => Base->IsVisible;
    public bool IsAddonReady => Base->IsVisible && Base->UldManager.LoadedState == FFXIVClientStructs.FFXIV.Component.GUI.AtkLoadState.Loaded && Base->IsFullyLoaded();

    public bool HasFocus
    {
        get
        {
            var focus = AtkStage.Instance()->GetFocus();
            if(focus == null) return false;
            for(var i = 0; i < RaptureAtkUnitManager.Instance()->FocusedUnitsList.Count; i++)
            {
                var atk = RaptureAtkUnitManager.Instance()->FocusedUnitsList.Entries[i].Value;
                if (atk != null && atk == Base)
                    return true;
            }
            return false;
        }
    }

    public bool IsAddonInFocusList
    {
        get
        {
            for(var i = 0; i < RaptureAtkUnitManager.Instance()->FocusedUnitsList.Count; i++)
            {
                var atk = RaptureAtkUnitManager.Instance()->FocusedUnitsList.Entries[i].Value;
                if(atk != null && atk == Base) return true;
            }
            return false;
        }
    }

    [Obsolete("For the intended functionality please use HasFocus. For the same functionality please use IsAddonInFocusList.")]
    public bool IsAddonFocused => IsAddonInFocusList;
    public bool IsAddonOnlyFocusListEntry => RaptureAtkUnitManager.Instance()->FocusedUnitsList.Count == 1 && RaptureAtkUnitManager.Instance()->FocusedUnitsList.Entries[0].Value == Base;

    protected bool ClickButtonIfEnabled(AtkComponentButton* button, bool respectHoldButtons = false)
    {
        if (button->IsEnabled && button->AtkResNode->IsVisible()
            && (!respectHoldButtons || button->GetComponentType() != ComponentType.HoldButton))
        {
            var owner = button->AtkComponentBase.OwnerNode;
            if (owner == null)
                return false;
            var evt = (AtkEvent*)owner->AtkResNode.AtkEventManager.Event;
            if (evt == null)
                return false;
            Base->AtkEventListener.ReceiveEvent(evt->State.EventType, (int)evt->Param, evt);
            return true;
        }
        return false;
    }

    protected bool ClickButtonIfEnabled(AtkComponentRadioButton* button)
    {
        if (button->IsEnabled && button->AtkResNode->IsVisible())
        {
            var owner = button->OwnerNode;
            if (owner == null)
                return false;
            var evt = (AtkEvent*)owner->AtkResNode.AtkEventManager.Event;
            if (evt == null)
                return false;
            Base->AtkEventListener.ReceiveEvent(evt->State.EventType, (int)evt->Param, evt);
            return true;
        }
        return false;
    }

    protected bool ClickCheckboxIfEnabled(AtkComponentCheckBox* checkbox)
    {
        if (checkbox->IsEnabled && checkbox->AtkResNode->IsVisible())
        {
            checkbox->SetChecked(true);
            var owner = checkbox->AtkComponentButton.AtkComponentBase.OwnerNode;
            if (owner == null)
                return false;
            var evt = (AtkEvent*)owner->AtkResNode.AtkEventManager.Event;
            if (evt == null)
                return false;
            Base->AtkEventListener.ReceiveEvent(evt->State.EventType, (int)evt->Param, evt);
            return true;
        }
        return false;
    }

    protected AtkEvent CreateAtkEvent(byte flags = 0)
    {
        var ret = stackalloc AtkEvent[]
        {
            new()
            {
                Listener = (AtkEventListener*)Base,
                Target = &AtkStage.Instance()->AtkEventTarget,
                State = new()
                {
                    StateFlags = (AtkEventStateFlags)flags
                }
            }
        };
        return *ret;
    }
}

public abstract unsafe class AddonMasterBase : AddonMasterBase<AtkUnitBase>
{
    protected AddonMasterBase(nint addon) : base(addon)
    {
    }

    protected AddonMasterBase(void* addon) : base(addon)
    {
    }
}

public unsafe interface IAddonMasterBase
{
    string AddonDescription { get; }
    unsafe AtkUnitBase* Base { get; }
    bool HasFocus { get; }
    bool IsAddonInFocusList { get; }
    bool IsAddonOnlyFocusListEntry { get; }
    bool IsAddonReady { get; }
    bool IsVisible { get; }
}
