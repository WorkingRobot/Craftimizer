using Craftimizer.Simulator;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Craftimizer.Plugin.Windows;

public sealed unsafe partial class Craft : Window, IDisposable
{
    // State variables, manually kept track of outside of the addon
    private CharacterStats CharacterStats = null!;
    private SimulationInput Input = null!;
    private int ActionCount;
    private ActionStates ActionStates;

    private sealed class AddonValues
    {
        public AddonSynthesis* Addon { get; }
        public AtkValue* Values => Addon->AtkUnitBase.AtkValues;
        public ushort ValueCount => Addon->AtkUnitBase.AtkValuesCount;

        public AddonValues(AddonSynthesis* addon)
        {
            Addon = addon;
            if (ValueCount != 26)
                throw new ArgumentException("AddonSynthesis must have 26 AtkValues", nameof(addon));
        }

        public unsafe AtkValue* this[int i] => Values + i;

        // Always 0?
        private uint Unk0 => GetUInt(0);
        // Always true?
        private bool Unk1 => GetBool(1);

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
        public Condition Condition => (Condition)(1 << (int)ConditionValue);
        public bool IsCollectible => IsCollectibleValue != 0;

        private uint GetUInt(int i)
        {
            var value = this[i];
            return value->Type == ValueType.UInt ?
                value->UInt :
                throw new ArgumentException($"Value {i} is not a uint", nameof(i));
        }

        private bool GetBool(int i)
        {
            var value = this[i];
            return value->Type == ValueType.Bool ?
                value->Byte != 0 :
                throw new ArgumentException($"Value {i} is not a boolean", nameof(i));
        }

        private SeString GetString(int i)
        {
            var value = this[i];
            return value->Type switch
            {
                ValueType.AllocatedString or
                ValueType.String =>
                    MemoryHelper.ReadSeStringNullTerminated((nint)value->String),
                _ => throw new ArgumentException($"Value {i} is not a string", nameof(i))
            };
        }
    }

    private const ushort StatusInnerQuiet = 251;
    private const ushort StatusWasteNot = 252;
    private const ushort StatusVeneration = 2226;
    private const ushort StatusGreatStrides = 254;
    private const ushort StatusInnovation = 2189;
    private const ushort StatusFinalAppraisal = 2190;
    private const ushort StatusWasteNot2 = 257;
    private const ushort StatusMuscleMemory = 2191;
    private const ushort StatusManipulation = 1164;
    private const ushort StatusHeartAndSoul = 2665;

    private SimulationState GetAddonSimulationState()
    {
        var player = Service.ClientState.LocalPlayer!;
        var values = new AddonValues(RecipeUtils.AddonSynthesis);
        var statusManager = ((Character*)player.Address)->GetStatusManager();

        byte GetEffectStack(ushort id)
        {
            foreach (var status in statusManager->StatusSpan)
                if (status.StatusID == id)
                    return status.StackCount;
            return 0;
        }
        bool HasEffect(ushort id)
        {
            foreach (var status in statusManager->StatusSpan)
                if (status.StatusID == id)
                    return true;
            return false;
        }

        return new(Input)
        {
            ActionCount = ActionCount,
            StepCount = (int)values.StepCount - 1,
            Progress = (int)values.Progress,
            Quality = (int)values.Quality,
            Durability = (int)values.Durability,
            CP = (int)player.CurrentCp,
            Condition = values.Condition,
            ActiveEffects = new()
            {
                InnerQuiet = GetEffectStack(StatusInnerQuiet),
                WasteNot = GetEffectStack(StatusWasteNot),
                Veneration = GetEffectStack(StatusVeneration),
                GreatStrides = GetEffectStack(StatusGreatStrides),
                Innovation = GetEffectStack(StatusInnovation),
                FinalAppraisal = GetEffectStack(StatusFinalAppraisal),
                WasteNot2 = GetEffectStack(StatusWasteNot2),
                MuscleMemory = GetEffectStack(StatusMuscleMemory),
                Manipulation = GetEffectStack(StatusManipulation),
                HeartAndSoul = HasEffect(StatusHeartAndSoul),
            },
            ActionStates = ActionStates
        };
    }
}

