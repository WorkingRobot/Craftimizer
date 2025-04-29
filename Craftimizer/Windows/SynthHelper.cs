using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using Sim = Craftimizer.Simulator.Simulator;
using SimNoRandom = Craftimizer.Simulator.SimulatorNoRandom;

namespace Craftimizer.Windows;

public sealed unsafe class SynthHelper : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlagsPinned = WindowFlagsFloating
      | ImGuiWindowFlags.NoSavedSettings;

    private const ImGuiWindowFlags WindowFlagsFloating = 
        ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoFocusOnAppearing;

    private const string WindowNamePinned = "Craftimizer Synthesis Helper###CraftimizerSynthHelper";
    private const string WindowNameFloating = $"{WindowNamePinned}Floating";

    public AddonSynthesis* Addon { get; private set; }
    public RecipeData? RecipeData { get; private set; }
    public CharacterStats? CharacterStats { get; private set; }
    public SimulationInput? SimulationInput { get; private set; }
    public ActionType? NextAction => (ShouldOpen && Macro.Count > 0) ? Macro[0].Action : null;
    public bool ShouldDrawAnts => ShouldOpen && !IsCollapsed;

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
    private SimulatedMacro Macro { get; } = new();

    private BackgroundTask<int>? SolverTask { get; set; }
    private bool SolverRunning => (!SolverTask?.Completed) ?? false;
    private Solver.Solver? SolverObject { get; set; }

    private IFontHandle AxisFont { get; }

    public SynthHelper() : base(WindowNamePinned)
    {
        AxisFont = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis14));

        Service.Plugin.Hooks.OnActionUsed += OnUseAction;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        IsOpen = true;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(494, -1),
            MaximumSize = new(494, 10000)
        };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => Service.Plugin.OpenSettingsTab("Synthesis Helper"),
                ShowTooltip = () => ImGuiUtils.Tooltip("Open Settings")
            },
            new() {
                Icon = FontAwesomeIcon.Heart,
                IconOffset = new(2, 1),
                Click = _ => Util.OpenLink(Plugin.Plugin.SupportLink),
                ShowTooltip = () => ImGuiUtils.Tooltip("Support me on Ko-fi!")
            }
        ];

        Service.WindowSystem.AddWindow(this);
    }

    private bool IsCollapsed { get; set; }
    private bool ShouldOpen { get; set; }

    private bool WasOpen { get; set; }
    private bool WasCollapsed { get; set; }

    private bool ShouldCalculate => !IsCollapsed && ShouldOpen;
    private bool WasCalculatable { get; set; }

    private bool IsRecalculateQueued { get; set; }

    public override void Update()
    {
        base.Update();

        ShouldOpen = CalculateShouldOpen();

        if (ShouldCalculate != WasCalculatable)
        {
            if (WasCalculatable)
                SolverTask?.Cancel();
            else if (Macro.Count == 0)
                RefreshCurrentState();
        }

        if (Macro.Count == 0 && ShouldOpen)
        {
            if (ShouldOpen != WasOpen || IsCollapsed != WasCollapsed)
                RefreshCurrentState();
        }

        if (!ShouldOpen)
        {
            StyleAlpha = LastAlpha = null;
            LastPosition = null;
        }

        WasOpen = ShouldOpen;
        WasCollapsed = IsCollapsed;
        WasCalculatable = ShouldCalculate;
    }

    public override bool DrawConditions() =>
        ShouldOpen;

    private bool wasInCraftAction;
    private bool CalculateShouldOpen()
    {
        if (Service.ClientState.LocalPlayer == null)
            return false;

        if (!Service.Configuration.EnableSynthHelper)
            return false;

        var recipeId = CSRecipeNote.Instance()->ActiveCraftRecipeId;

        if (recipeId == 0)
        {
            RecipeData = null;
            return false;
        }

        Addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis");

        if (Addon == null)
        {
            RecipeData = null;
            return false;
        }

        // Check if Synthesis addon is visible
        if (Addon->AtkUnitBase.WindowNode == null)
            return false;

        if (Service.Configuration.DisableSynthHelperOnMacro)
        {
            var module = RaptureShellModule.Instance();
            if (module->MacroCurrentLine >= 0)
            {
                var hasCraftAction = false;
                foreach (ref var line in module->MacroLines)
                {
                    if (line.EqualToString("/craftaction"))
                    {
                        hasCraftAction = true;
                        break;
                    }
                }
                if (!hasCraftAction)
                    return false;
            }
        }

        if (RecipeData?.RecipeId != recipeId)
        {
            OnStartCrafting(recipeId);
            OnStateUpdated();
        }

        if (IsRecalculateQueued)
            OnStateUpdated();

        Macro.FlushQueue();

        var isInCraftAction = Service.Condition[ConditionFlag.Crafting40];
        if (!isInCraftAction && wasInCraftAction)
            RefreshCurrentState();
        wasInCraftAction = isInCraftAction;

        return true;
    }

    private Vector2? LastPosition { get; set; }
    private byte? StyleAlpha { get; set; }
    private byte? LastAlpha { get; set; }
    public override void PreDraw()
    {
        base.PreDraw();

        IsCollapsed = true;

        if (Service.Configuration.PinSynthHelperToWindow)
        {
            ref var unit = ref Addon->AtkUnitBase;
            var scale = unit.Scale;
            var pos = new Vector2(unit.X, unit.Y);
            var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

            var offset = 5;

            var newAlpha = unit.WindowNode->AtkResNode.Alpha_2;
            StyleAlpha = LastAlpha ?? newAlpha;
            LastAlpha = newAlpha;

            var newPosition = pos + new Vector2(size.X, offset * scale);
            Position = ImGuiHelpers.MainViewport.Pos + (LastPosition ?? newPosition);
            LastPosition = newPosition;
            Flags = WindowFlagsPinned;
            WindowName = WindowNamePinned;
        }
        else
        {
            StyleAlpha = LastAlpha = null;
            Position = LastPosition = null;
            Flags = WindowFlagsFloating;
            WindowName = WindowNameFloating;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, StyleAlpha.HasValue ? (StyleAlpha.Value / 255f) : 1);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();

        base.PostDraw();
    }

    public override void Draw()
    {
        IsCollapsed = false;

        DrawMacro();

        DrawMacroInfo();

        ImGuiHelpers.ScaledDummy(5);

        DrawMacroActions();

        if (SolverRunning && SolverObject is { } solver)
        {
            ImGuiHelpers.ScaledDummy(5);
            DynamicBars.DrawProgressBar(solver);
        }
    }

    private SimulationState? hoveredState;
    private SimulationState DisplayedState => hoveredState ?? (Service.Configuration.SynthHelperDisplayOnlyFirstStep ? Macro.FirstState : Macro.State);
    private void DrawMacro()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var imageSize = ImGui.GetFrameHeight() * 2;
        var canExecute = !Service.Condition[ConditionFlag.Crafting40];
        var lastState = Macro.InitialState;
        hoveredState = null;

        var itemsPerRow = (int)Math.Max(1, MathF.Floor((ImGui.GetContentRegionAvail().X + spacing) / (imageSize + spacing)));

        using var _color = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero);
        using var _color3 = ImRaii.PushColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        using var _color2 = ImRaii.PushColor(ImGuiCol.ButtonActive, Vector4.Zero);
        var count = Macro.Count;
        for (var i = 0; i < count; i++)
        {
            if (i % itemsPerRow != 0)
                ImGui.SameLine(0, spacing);
            var (action, response, state) = (Macro[i].Action, Macro[i].Response, Macro[i].State);
            var actionBase = action.Base();
            var failedAction = response != ActionResponse.UsedAction;
            using var _id = ImRaii.PushId(i);
            if (i == 0)
            {
                var pos = ImGui.GetCursorScreenPos();
                var offsetVec2 = ImGui.GetStyle().ItemSpacing / 2;
                var offset = new Vector2((offsetVec2.X + offsetVec2.Y) / 2f);
                var color = canExecute ? ImGuiColors.DalamudWhite2 : ImGuiColors.DalamudGrey3;
                ImGui.GetWindowDrawList().AddRectFilled(pos - offset, pos + new Vector2(imageSize) + offset, ImGui.GetColorU32(color), 4);
            }
            bool isHovered, isHeld, isPressed;
            {
                var pos = ImGui.GetCursorScreenPos();
                var offset = ImGui.GetStyle().ItemSpacing / 2f;
                var size = new Vector2(imageSize);

                // yoinked from https://github.com/goatcorp/Dalamud/blob/48e8462550141db9b1a153cab9548e60238500c7/Dalamud/Interface/Windowing/Window.cs#L551
                var min = pos - offset;
                var max = pos + size + offset;
                var bb = new Vector4(min.X, min.Y, max.X, max.Y);

                var id = ImGui.GetID($"###ButtonContainer");
                var isClipped = !ImGuiExtras.ItemAdd(bb, id, out _, 0);
                
                isPressed = ImGuiExtras.ButtonBehavior(bb, id, out isHovered, out isHeld, ImGuiButtonFlags.None);
            }
            ImGui.ImageButton(action.GetIcon(RecipeData!.ClassJob).ImGuiHandle, new(imageSize), default, Vector2.One, 0, default, failedAction ? new(1, 1, 1, ImGui.GetStyle().DisabledAlpha) : Vector4.One);
            if (isPressed && i == 0)
            {
                if (ExecuteNextAction())
                    break;
            }
            if (isHovered)
            {
                ImGuiUtils.Tooltip($"{action.GetName(RecipeData!.ClassJob)}\n" +
                    $"{actionBase.GetTooltip(CreateSim(lastState), true)}" +
                    $"{(canExecute && i == 0 ? "Click or run /craftaction to execute" : string.Empty)}");
                hoveredState = state;
            }
            lastState = state;
        }

        var rows = (int)Math.Max(1, MathF.Ceiling(Service.Configuration.SynthHelperMaxDisplayCount / itemsPerRow));
        for (var i = 0; i < rows; ++i)
        {
            if (count <= i * itemsPerRow)
                ImGui.Dummy(new(0, imageSize));
        }
    }

    private void DrawMacroInfo()
    {
        var state = DisplayedState;

        using (var panel = ImRaii2.GroupPanel("Buffs", -1, out _))
        {
            using var _font = AxisFont.Push();

            var iconHeight = ImGui.GetFrameHeight() * 1.75f;
            var durationShift = iconHeight * .2f;

            ImGui.Dummy(new(0, iconHeight + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetTextLineHeight() - durationShift));
            ImGui.SameLine(0, 0);

            var effects = state.ActiveEffects;
            foreach (var effect in Enum.GetValues<EffectType>())
            {
                if (!effects.HasEffect(effect))
                    continue;

                using (var group = ImRaii.Group())
                {
                    var icon = effect.GetIcon(effects.GetStrength(effect));
                    var size = new Vector2(iconHeight * (icon.AspectRatio ?? 1), iconHeight);

                    ImGui.Image(icon.ImGuiHandle, size);
                    if (!effect.IsIndefinite())
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - durationShift);
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 1);
                        ImGuiUtils.TextCentered($"{effects.GetDuration(effect)}", size.X);
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    var status = effect.Status();
                    using var _reset = ImRaii.DefaultFont();
                    ImGuiUtils.Tooltip($"{status.Name.ExtractText()}\n{status.Description.ExtractText()}");
                }
                ImGui.SameLine();
            }
        }

        var reliability = Macro.GetReliability(RecipeData!, Service.Configuration.SynthHelperDisplayOnlyFirstStep ? 0 : ^1);
        {
            var mainBars = new List<DynamicBars.BarData>()
            {
                new("Progress", Colors.Progress, reliability.Progress, state.Progress, RecipeData!.RecipeInfo.MaxProgress),
                new("Quality", Colors.Quality, reliability.Quality, state.Quality, RecipeData.RecipeInfo.MaxQuality),
                new("CP", Colors.CP, state.CP, CharacterStats!.CP),
            };
            if (RecipeData.RecipeInfo.MaxQuality <= 0)
                mainBars.RemoveAt(1);
            var halfBars = new List<DynamicBars.BarData>()
            {
                new("Durability", Colors.Durability, state.Durability, RecipeData.RecipeInfo.MaxDurability),
            };
            if (RecipeData.IsCollectable)
                halfBars.Add(new("Collectability", Colors.Collectability, reliability.ParamScore, state.Collectability, state.MaxCollectability, RecipeData.CollectableThresholds, $"{state.Collectability}", $"{state.MaxCollectability:0}"));
            else if (RecipeData.Recipe.RequiredQuality > 0)
            {
                var qualityPercent = (float)state.Quality / RecipeData.Recipe.RequiredQuality * 100;
                halfBars.Add(new("Quality %", Colors.HQ, reliability.ParamScore, qualityPercent, 100, null, $"{qualityPercent:0}%", null));
            }
            else if (RecipeData.RecipeInfo.MaxQuality > 0)
                halfBars.Add(new("HQ %", Colors.HQ, reliability.ParamScore, state.HQPercent, 100, null, $"{state.HQPercent}%", null));

            if (halfBars.Count > 1)
            {
                var textSize = DynamicBars.GetTextSize(mainBars.Concat(halfBars));
                DynamicBars.Draw(mainBars, textSize);
                using var table = ImRaii.Table($"##{nameof(SynthHelper)}_halfbars", halfBars.Count, ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.SizingStretchSame);
                if (table)
                {
                    foreach (var bar in halfBars)
                    {
                        ImGui.TableNextColumn();
                        DynamicBars.Draw(new[] { bar });
                    }
                }
            }
            else
            {
                DynamicBars.Draw(mainBars.Concat(halfBars));
            }
        }
    }

    private void DrawMacroActions()
    {
        if (SolverRunning)
        {
            if (SolverTask?.Cancelling ?? false)
            {
                using var _disabled = ImRaii.Disabled();
                ImGui.Button("Stopping", new(-1, 0));
                if (ImGui.IsItemHovered())
                    ImGuiUtils.TooltipWrapped("This might could a while, sorry! Please report if this takes longer than a second.");
            }
            else
            {
                if (ImGui.Button("Stop", new(-1, 0)))
                    SolverTask?.Cancel();
            }
        }
        else
        {
            if (ImGui.Button("Retry", new(-1, 0)))
                AttemptRetry();
            if (ImGui.IsItemHovered())
                ImGuiUtils.TooltipWrapped("Suggest a way to finish the crafting recipe. " +
                                 "Results aren't perfect, and levels of success " +
                                 "can vary wildly depending on the solver's settings.");
        }

        if (ImGui.Button("Open in Macro Editor", new(-1, 0)))
            Service.Plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.ClientState.LocalPlayer!.StatusList), null, [], null);
    }

    public bool ExecuteNextAction()
    {
        var canExecute = !Service.Condition[ConditionFlag.Crafting40];
        var action = NextAction;
        if (canExecute && action != null)
        {
            Chat.SendMessage($"/ac \"{action.Value.GetName(RecipeData!.ClassJob)}\"");
            return true;
        }
        return false;
    }

    public void AttemptRetry()
    {
        if (!SolverRunning)
            CalculateBestMacro();
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
        if (!ShouldOpen || IsCollapsed)
            return;

        (_, CurrentState) = new SimNoRandom().Execute(GetCurrentState(), action);
        CurrentActionCount = CurrentState.ActionCount;
        CurrentActionStates = CurrentState.ActionStates;
    }

    private void RefreshCurrentState() =>
        CurrentState = GetCurrentState();

    private SimulationState GetCurrentState()
    {
        var player = Service.ClientState.LocalPlayer!;
        var values = new SynthesisValues(Addon);
        var statusManager = ((Character*)player.Address)->GetStatusManager();

        byte GetEffectStack(ushort id)
        {
            foreach (var status in statusManager->Status)
                if (status.StatusId == id)
                    return (byte)status.Param;
            return 0;
        }
        bool HasEffect(ushort id)
        {
            foreach (var status in statusManager->Status)
                if (status.StatusId == id)
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
                Expedience = GetEffectStack((ushort)EffectType.Expedience.StatusId()),
                TrainedPerfection = HasEffect((ushort)EffectType.TrainedPerfection.StatusId()),
                HeartAndSoul = HasEffect((ushort)EffectType.HeartAndSoul.StatusId()),
            },
            ActionStates = CurrentActionStates
        };
    }

    private void OnStateUpdated()
    {
        if (!ShouldOpen || IsCollapsed)
        {
            IsRecalculateQueued = true;
            return;
        }

        IsRecalculateQueued = false;
        Macro.Clear();
        Macro.InitialState = CurrentState;
        CalculateBestMacro();
    }

    private void CalculateBestMacro()
    {
        SolverTask?.Cancel();
        Macro.ClearQueue();
        Macro.Clear();

        if (Service.Configuration.ConditionRandomness)
        {
            Service.Configuration.ConditionRandomness = false;
            Service.Configuration.Save();
            Macro.RecalculateState();
        }

        var state = CurrentState;
        SolverTask = new(token => CalculateBestMacroTask(state, token, Gearsets.HasDelineations()));
        SolverTask.Start();
    }

    private int CalculateBestMacroTask(SimulationState state, CancellationToken token, bool hasDelineations)
    {
        var config = Service.Configuration.SynthHelperSolverConfig;
        var canUseDelineations = !Service.Configuration.CheckDelineations || hasDelineations;
        if (!canUseDelineations)
            config = config.FilterSpecialistActions();

        token.ThrowIfCancellationRequested();

        var solver = new Solver.Solver(config, state) { Token = token };
        solver.OnLog += Log.Debug;
        solver.OnWarn += t => Service.Plugin.DisplaySolverWarning(t);
        solver.OnNewAction += EnqueueAction;
        SolverObject = solver;
        solver.Start();
        _ = solver.GetTask().GetAwaiter().GetResult();

        token.ThrowIfCancellationRequested();

        return 0;
    }

    private void EnqueueAction(ActionType action)
    {
        var newSize = Macro.Enqueue(action, Service.Configuration.SynthHelperMaxDisplayCount);
        if (newSize >= Service.Configuration.SynthHelperStepCount || newSize >= Service.Configuration.SynthHelperMaxDisplayCount)
            SolverTask?.Cancel();
    }

    private static Sim CreateSim(in SimulationState state) =>
        Service.Configuration.ConditionRandomness ? new Sim() { State = state } : new SimNoRandom() { State = state };

    public void Dispose()
    {
        Service.Plugin.Hooks.OnActionUsed -= OnUseAction;

        Service.WindowSystem.RemoveWindow(this);

        AxisFont.Dispose();
    }
}
