// Vendored from ECommons (https://github.com/NightmareXIV/ECommons) — MIT License,
// Copyright (c) 2023 NightmareXIV. See LeveDeliver/ECommonsVendored/VENDORED.md.
//
// Adapted for the vendored build: services come from ECommons.Services, the
// hook-based logging detour (InstallHook/UninstallHook) is omitted since
// LeveDeliver only needs Fire, and ECommons extension methods (.Join, .Log)
// are inlined.

using Dalamud.Hooking;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;
#nullable disable

namespace ECommons.Automation;

public static unsafe class Callback
{
    public static readonly AtkValue ZeroAtkValue = new() { Type = 0, Int = 0 };

    public static void Fire(AtkUnitBase* Base, bool updateState, params object[] values)
    {
        if (Base == null) throw new Exception("Null UnitBase");
        var atkValues = (AtkValue*)Marshal.AllocHGlobal(values.Length * sizeof(AtkValue));
        if (atkValues == null) return;
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                var v = values[i];
                switch (v)
                {
                    case uint uintValue:
                        atkValues[i].Type = ValueType.UInt;
                        atkValues[i].UInt = uintValue;
                        break;
                    case int intValue:
                        atkValues[i].Type = ValueType.Int;
                        atkValues[i].Int = intValue;
                        break;
                    case float floatValue:
                        atkValues[i].Type = ValueType.Float;
                        atkValues[i].Float = floatValue;
                        break;
                    case bool boolValue:
                        atkValues[i].Type = ValueType.Bool;
                        atkValues[i].Byte = (byte)(boolValue ? 1 : 0);
                        break;
                    case string stringValue:
                        {
                            atkValues[i].Type = ValueType.String;
                            var stringBytes = Encoding.UTF8.GetBytes(stringValue);
                            var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                            Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length);
                            Marshal.WriteByte(stringAlloc, stringBytes.Length, 0);
                            atkValues[i].String = (byte*)stringAlloc;
                            break;
                        }
                    case AtkValue rawValue:
                        {
                            atkValues[i] = rawValue;
                            break;
                        }
                    default:
                        throw new ArgumentException($"Unable to convert type {v.GetType()} to AtkValue");
                }
            }
            Base->FireCallback((uint)values.Length, atkValues, updateState);
        }
        finally
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (atkValues[i].Type == ValueType.String)
                {
                    Marshal.FreeHGlobal(new IntPtr(atkValues[i].String));
                }
            }
            Marshal.FreeHGlobal(new IntPtr(atkValues));
        }
    }
}
