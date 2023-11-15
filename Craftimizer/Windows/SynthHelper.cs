using Craftimizer.Plugin;
using Craftimizer.Plugin.Utils;
using Craftimizer.Simulator;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using System;
using System.Threading;
using Craftimizer.Utils;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using Dalamud.Interface.Utility;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;

namespace Craftimizer.Windows;

public sealed unsafe class SynthHelper : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration
      | ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoSavedSettings
      | ImGuiWindowFlags.NoFocusOnAppearing
      | ImGuiWindowFlags.NoNavFocus;

    public AddonSynthesis* Addon { get; private set; }
    public RecipeData? RecipeData { get; private set; }
    public CharacterStats? CharacterStats { get; private set; }
    public SimulationInput? SimulationInput { get; private set; }

    public bool IsCrafting { get; private set; }
    private int CurrentActionCount { get; set; }
    private ActionStates CurrentActionStates { get; set; }
    private SimulationState CurrentState
    {
        get => currentState;
        set
        {
            if (currentState != value)
            {
                currentState = value;
                OnStateUpdated();
            }
        }
    }
    private SimulationState currentState;

    private CancellationTokenSource? HelperTaskTokenSource { get; set; }
    private Exception? HelperTaskException { get; set; }

    public SynthHelper() : base("Craftimizer SynthHelper", WindowFlags)
    {
        Service.Plugin.Hooks.OnActionUsed += OnUseAction;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        IsOpen = true;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(-1),
            MaximumSize = new(10000, 10000)
        };

        Service.WindowSystem.AddWindow(this);
    }

    private bool wasInCraftAction;
    public override void Update()
    {
        Addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis");

        if (Addon != null)
        {
            var agent = AgentRecipeNote.Instance();
            var recipeId = (ushort)agent->ActiveCraftRecipeId;

            if (agent->ActiveCraftRecipeId == 0)
                IsCrafting = false;
            else if (!IsCrafting)
            {
                IsCrafting = true;
                OnStartCrafting(recipeId);
            }
        }
        else
            IsCrafting = false;

        var isInCraftAction = Service.Condition[ConditionFlag.Crafting40];
        if (!isInCraftAction && wasInCraftAction)
            OnFinishedUsingAction();
        wasInCraftAction = isInCraftAction;
    }
    
    private bool wasOpen;
    public override bool DrawConditions()
    {
        var isOpen = ShouldDraw();
        if (isOpen != wasOpen)
        {
            if (wasOpen)
                HelperTaskTokenSource?.Cancel();
        }

        wasOpen = isOpen;
        return isOpen;
    }

    private bool ShouldDraw()
    {
        if (Service.ClientState.LocalPlayer == null)
            return false;

        if (Addon == null)
            return false;

        if (!IsCrafting)
            return false;

        // Check if Synthesis addon is visible
        if (Addon->AtkUnitBase.WindowNode == null)
            return false;

        return true;
    }

    public override void PreDraw()
    {
        ref var unit = ref Addon->AtkUnitBase;
        var scale = unit.Scale;
        var pos = new Vector2(unit.X, unit.Y);
        var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;
        
        var node = unit.GetNodeById(46);

        Position = ImGuiHelpers.MainViewport.Pos + pos + new Vector2(size.X, node->Y * scale);
    }

    public override void Draw()
    {
        ImGui.Text($"{IsCrafting} {CurrentState.Progress} {CurrentState.ActionCount} {CurrentState.Condition}");
    }

    private void OnStartCrafting(ushort recipeId)
    {
        var shouldUpdateInput = false;
        if (recipeId != RecipeData?.RecipeId)
        {
            RecipeData = new(recipeId);
            shouldUpdateInput = true;
        }

        {
            var gearStats = Gearsets.CalculateGearsetCurrentStats();

            var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            if (container == null)
                throw new InvalidOperationException("Could not get inventory container");

            var gearItems = Gearsets.GetGearsetItems(container);

            var characterStats = Gearsets.CalculateCharacterStats(gearStats, gearItems, RecipeData.ClassJob.GetPlayerLevel(), RecipeData.ClassJob.CanPlayerUseManipulation());
            if (characterStats != CharacterStats)
            {
                CharacterStats = characterStats;
                shouldUpdateInput = true;
            }
        }

        if (shouldUpdateInput)
            SimulationInput = new(CharacterStats, RecipeData.RecipeInfo);

        CurrentActionCount = 0;
        CurrentActionStates = new();
        CurrentState = GetCurrentState();
    }

    private void OnUseAction(ActionType action)
    {
        if (!IsCrafting)
            return;

        (_, CurrentState) = new SimulatorNoRandom().Execute(GetCurrentState(), action);
        CurrentActionCount = CurrentState.ActionCount;
        CurrentActionStates = CurrentState.ActionStates;
    }

    private void OnFinishedUsingAction()
    {
        if (!IsCrafting)
            return;

        CurrentState = GetCurrentState();
    }

    private SimulationState GetCurrentState()
    {
        var player = Service.ClientState.LocalPlayer!;
        var values = new SynthesisValues(Addon);
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

        return new(SimulationInput!)
        {
            ActionCount = CurrentActionCount,
            StepCount = (int)values.StepCount - 1,
            Progress = (int)values.Progress,
            Quality = (int)values.Quality,
            Durability = (int)values.Durability,
            CP = (int)player.CurrentCp,
            Condition = values.Condition,
            ActiveEffects = new()
            {
                InnerQuiet = GetEffectStack((ushort)EffectType.InnerQuiet.StatusId()),
                WasteNot = GetEffectStack((ushort)EffectType.WasteNot.StatusId()),
                Veneration = GetEffectStack((ushort)EffectType.Veneration.StatusId()),
                GreatStrides = GetEffectStack((ushort)EffectType.GreatStrides.StatusId()),
                Innovation = GetEffectStack((ushort)EffectType.Innovation.StatusId()),
                FinalAppraisal = GetEffectStack((ushort)EffectType.FinalAppraisal.StatusId()),
                WasteNot2 = GetEffectStack((ushort)EffectType.WasteNot2.StatusId()),
                MuscleMemory = GetEffectStack((ushort)EffectType.MuscleMemory.StatusId()),
                Manipulation = GetEffectStack((ushort)EffectType.Manipulation.StatusId()),
                HeartAndSoul = HasEffect((ushort)EffectType.HeartAndSoul.StatusId()),
            },
            ActionStates = CurrentActionStates
        };
    }

    private void OnStateUpdated()
    {
        if (!IsCrafting)
            return;

        Log.Debug("state updated!");
    }

    public void Dispose()
    {
        Service.Plugin.Hooks.OnActionUsed -= OnUseAction;

        Service.WindowSystem.RemoveWindow(this);
    }
}
