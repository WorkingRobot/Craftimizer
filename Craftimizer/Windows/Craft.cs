using Craftimizer.Plugin.Utils;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Craftimizer.Plugin.Windows;

public sealed unsafe class Craft : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration
      | ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoSavedSettings
      | ImGuiWindowFlags.NoFocusOnAppearing
      | ImGuiWindowFlags.NoNavFocus;

    private static Configuration Config => Service.Configuration;

    private static Random Random { get; } = new();
    private static RecipeNote RecipeUtils => Service.Plugin.RecipeNote;

    private bool WasOpen { get; set; }

    private CharacterStats CharacterStats { get; set; } = null!;
    private SimulationInput Input { get; set; } = null!;
    private int ActionCount { get; set; }
    private ActionStates ActionStates { get; set; }

    // Set to true if we used an action, but it's not reflected in the addon yet
    private bool IsIntermediate { get; set; }
    private SimulationState IntermediateState { get; set; }

    private SimulationState? SolverState { get; set; }
    private Task? SolverTask { get; set; }
    private CancellationTokenSource? SolverTaskToken { get; set; }
    private ConcurrentQueue<ActionType> SolverActionQueue { get; } = new();

    // State is the state of the simulation *after* its corresponding action is executed.
    private List<(ActionType Action, string Tooltip, ActionResponse Response, SimulationState State)> SolverActions { get; } = new();
    private SimulatorNoRandom SolverSim { get; set; } = null!;

    private SimulationState SolverLatestState => SolverActions.Count == 0 ? SolverState!.Value : SolverActions[^1].State;

    public Craft() : base("Craftimizer SynthesisHelper", WindowFlags, true)
    {
        Service.WindowSystem.AddWindow(this);
        Service.Plugin.Hooks.OnActionUsed += OnActionUsed;

        IsOpen = true;
    }

    public override void Draw()
    {
        while (SolverActionQueue.TryDequeue(out var poppedAction))
            AppendGeneratedAction(poppedAction);

        DrawActions();

        ImGui.Dummy(default);
        ImGui.BeginDisabled(!(SolverTask?.IsCompleted ?? true) || IsIntermediate);
            if (ImGui.Button("Retry"))
                QueueSolve(CreateSimulationState());
        ImGui.EndDisabled();
    }

    private void DrawActions()
    {
        var totalWidth = 300f;
        var actionsPerRow = 5;

        var actionSize = new Vector2((totalWidth / actionsPerRow) - (ImGui.GetStyle().ItemSpacing.X * ((actionsPerRow + 1f) / actionsPerRow)));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);

        ImGui.Dummy(new(0, actionSize.Y));
        ImGui.SameLine(0, 0);
        for (var i = 0; i < SolverActions.Count; ++i)
        {
            var (action, tooltip, _, state) = SolverActions[i];
            ImGui.PushID(i);
            if (ImGui.ImageButton(action.GetIcon(RecipeUtils.ClassJob).ImGuiHandle, actionSize, Vector2.Zero, Vector2.One, 0))
            {
                if (i == 0)
                    Chat.SendMessage($"/ac \"{action.GetName(RecipeUtils.ClassJob)}\"");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text($"{action.GetName(RecipeUtils.ClassJob)}\n{tooltip}");
                Simulator.DrawAllProgressTooltips(state);
                if (i == 0)
                    ImGui.Text("Click to Execute");
                ImGui.EndTooltip();
            }
            ImGui.PopID();
            if (i % actionsPerRow != (actionsPerRow - 1))
                ImGui.SameLine();
        }

        ImGui.PopStyleColor(3);
    }

    public override void PreDraw()
    {
        var addon = RecipeUtils.AddonSynthesis;
        ref var unit = ref addon->AtkUnitBase;
        var scale = unit.Scale;
        var pos = new Vector2(unit.X, unit.Y);
        var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

        var node = unit.GetNodeById(79);

        Position = pos + new Vector2(size.X, node->Y * scale);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(-1),
            MaximumSize = new(10000, 10000)
        };

        if (Input == null)
            return;

        var addonState = CreateSimulationState();
        if (IsIntermediate)
        {
            if (StatesEqualExceptSome(addonState, IntermediateState))
                return;

            IsIntermediate = false;
        }

        if (SolverState != addonState)
            QueueSolve(addonState);

        base.PreDraw();
    }

    private bool DrawConditionsInner()
    {
        if (!RecipeUtils.HasValidRecipe)
            return false;

        if (RecipeUtils.AddonSynthesis == null)
            return false;

        // Check if Synthesis addon is visible
        if (RecipeUtils.AddonSynthesis->AtkUnitBase.WindowNode == null)
            return false;

        if (RecipeUtils.AddonSynthesis->AtkUnitBase.GetNodeById(79) == null)
            return false;

        return base.DrawConditions();
    }

    public override bool DrawConditions()
    {
        if (!Config.EnableSynthesisHelper)
            return false;

        var ret = DrawConditionsInner();
        if (ret && !WasOpen)
            ResetSimulation();

        WasOpen = ret;
        return ret;
    }

    private void StopSolve()
    {
        if (SolverTask == null || SolverTaskToken == null)
            return;

        if (!SolverTask.IsCompleted)
            SolverTaskToken.Cancel();
        else
        {
            SolverTaskToken.Dispose();
            SolverTask.Dispose();

            SolverTask = null;
            SolverTaskToken = null;
        }
    }

    private void QueueSolve(SimulationState state)
    {
        StopSolve();

        SolverActionQueue.Clear();
        SolverActions.Clear();
        SolverState = state;
        SolverSim = new(state);

        SolverTaskToken = new();
        SolverTask = Task.Run(() => Config.SolverAlgorithm.Invoke(Config.SolverConfig, state, SolverActionQueue.Enqueue, SolverTaskToken.Token));
    }

    private void AppendGeneratedAction(ActionType action)
    {
        var actionBase = action.Base();
        if (actionBase is BaseComboAction comboActionBase)
        {
            AppendGeneratedAction(comboActionBase.ActionTypeA);
            AppendGeneratedAction(comboActionBase.ActionTypeB);
        }
        else
        {
            if (SolverActions.Count >= Config.SynthesisHelperStepCount)
            {
                StopSolve();
                return;
            }

            var tooltip = actionBase.GetTooltip(SolverSim, false);
            var (response, state) = SolverSim.Execute(SolverLatestState, action);
            SolverActions.Add((action, tooltip, response, state));

            if (SolverActions.Count >= Config.SynthesisHelperStepCount)
            {
                StopSolve();
                return;
            }
        }
    }

    private void ResetSimulation()
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            return;

        CharacterStats = Gearsets.CalculateCharacterStats(Gearsets.CalculateGearsetCurrentStats(), Gearsets.GetGearsetItems(container), RecipeUtils.CharacterLevel, RecipeUtils.CanUseManipulation);
        Input = new(CharacterStats, RecipeUtils.Info, 0, Random);
        ActionCount = 0;
        ActionStates = new();
        IsIntermediate = false;
    }

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

    private SimulationState CreateSimulationState()
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

    private void OnActionUsed(ActionType action)
    {
        if (RecipeUtils.AddonSynthesis == null)
            return;
        var inGameState = CreateSimulationState();
        (_, var predictedState) = new SimulatorNoRandom(inGameState).Execute(inGameState, action);
        QueueSolve(predictedState);

        ActionCount++;
        var states = ActionStates;
        states.MutateState(action.Base());
        ActionStates = states;
        IsIntermediate = true;
        IntermediateState = CreateSimulationState();
    }

    private static bool StatesEqualExceptSome(SimulationState a, SimulationState b)
    {
        b.CP = a.CP;
        b.ActiveEffects = a.ActiveEffects;
        return a == b;
    }

    public void Dispose()
    {
        StopSolve();
        SolverTask?.Wait();
        SolverTask?.Dispose();
        SolverTaskToken?.Dispose();

        Service.Plugin.Hooks.OnActionUsed -= OnActionUsed;
    }
}
