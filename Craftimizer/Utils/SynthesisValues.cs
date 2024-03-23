using Craftimizer.Simulator;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Craftimizer.Utils;

internal sealed unsafe class SynthesisValues(AddonSynthesis* addon)
{
    private AddonSynthesis* Addon { get; } = addon;

    private ReadOnlySpan<AtkValue> Values => new(Addon->AtkUnitBase.AtkValues, Addon->AtkUnitBase.AtkValuesCount);

    // Always 0?
    private uint Unk0 => GetUInt(0);
    private bool IsTrialSynthesis => TryGetBool(1) ?? false;
    public SeString ItemName => GetString(2);
    public uint ItemIconId => GetUInt(3);
    public uint ItemCount => GetUInt(4);
    public uint Progress => GetUInt(5);
    public uint MaxProgress => GetUInt(6);
    public uint Durability => GetUInt(7);
    public uint MaxDurability => GetUInt(8);
    public uint Quality => GetUInt(9);
    public uint HQChance => GetUInt(10);
    private uint IsShowingCollectibleInfoValue => GetUInt(11);
    private uint ConditionValue => GetUInt(12);
    public SeString ConditionName => GetString(13);
    public SeString ConditionNameAndTooltip => GetString(14);
    public uint StepCount => GetUInt(15);
    public uint ResultItemId => GetUInt(16);
    public uint MaxQuality => GetUInt(17);
    public uint RequiredQuality => GetUInt(18);
    private uint IsCollectibleValue => GetUInt(19);
    public uint Collectability => GetUInt(20);
    public uint MaxCollectability => GetUInt(21);
    public uint CollectabilityCheckpoint1 => GetUInt(22);
    public uint CollectabilityCheckpoint2 => GetUInt(23);
    public uint CollectabilityCheckpoint3 => GetUInt(24);
    public bool IsExpertRecipe => GetBool(25);

    public bool IsShowingCollectibleInfo => IsShowingCollectibleInfoValue != 0;
    public Condition Condition => (Condition)ConditionValue;
    public bool IsCollectible => IsCollectibleValue != 0;

    private uint? TryGetUInt(int i)
    {
        var value = Values[i];
        return value.Type == ValueType.UInt ?
            value.UInt :
            null;
    }

    private bool? TryGetBool(int i)
    {
        var value = Values[i];
        return value.Type == ValueType.Bool ?
            value.Byte != 0 :
            null;
    }

    private SeString? TryGetString(int i)
    {
        var value = Values[i];
        return value.Type switch
        {
            ValueType.AllocatedString or
            ValueType.String =>
                MemoryHelper.ReadSeStringNullTerminated((nint)value.String),
            _ => null
        };
    }

    private uint GetUInt(int i) =>
        TryGetUInt(i) ?? throw new ArgumentException($"Value {i} is not a uint", nameof(i));

    private bool GetBool(int i) =>
        TryGetBool(i) ?? throw new ArgumentException($"Value {i} is not a boolean", nameof(i));

    private SeString GetString(int i) =>
        TryGetString(i) ?? throw new ArgumentException($"Value {i} is not a string", nameof(i));
}
