using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using ActionCategory = Craftimizer.Simulator.ActionCategory;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Plugin.Windows;

public class SimulatorWindow : Window
{
    private static readonly Vector2 ProgressBarSize = new(200, 20);
    private static readonly Vector2 TooltipProgressBarSize = new(100, 5);

    private static readonly Vector4 ProgressColor   = new(.2f, 1f, .2f, 1f);
    private static readonly Vector4 QualityColor    = new(.2f, .2f, 1f, 1f);
    private static readonly Vector4 DurabilityColor = new(1f, 1f, .2f, 1f);
    private static readonly Vector4 CPColor         = new(1f, .2f, 1f, 1f);

    private static readonly Vector4 CPColorNew      = new(0.38f, 0.77f, 1f, 1f);

    private static readonly Vector4 BadActionImageTint = new(1f, .5f, .5f, 1f);
    private static readonly Vector4 BadActionImageColor = new(1f, .3f, .3f, 1f);

    private static readonly Vector4 BadActionTextColor = new(1f, .2f, .2f, 1f);

    private static readonly (ActionCategory Category, ActionType[] Actions)[] SortedActions;

    private TimeSpan FrameTime { get; set; }
    private Stopwatch Stopwatch { get; } = new();

    private Item Item { get; }
    private SimulationInput Input { get; }
    private ClassJob ClassJob { get; }
    // State is the state of the simulation *after* its corresponding action is executed.
    private List<(ActionType Action, string Tooltip, ActionResponse Response, SimulationState State)> Actions { get; }
    private Simulator.Simulator Simulator { get; }

    private SimulationState LatestState => Actions.Count == 0 ? new(Input) : Actions[^1].State;

    private ActionType? DraggedAction { get; set; }

    static SimulatorWindow()
    {
        SortedActions = Enum.GetValues<ActionType>().GroupBy(a => a.Category()).Select(g => (g.Key, g.OrderBy(a => a.Level()).ToArray())).ToArray();
    }

    public SimulatorWindow(Item item, SimulationInput input, ClassJob classJob, List<ActionType> actions) : base("Simulator")
    {
        Service.WindowSystem.AddWindow(this);

        Item = item;
        Input = input;
        ClassJob = classJob;
        Actions = new();
        Simulator = Service.Configuration.CreateSimulator(new(input));

        foreach(var action in actions)
            AppendAction(action);

        IsOpen = true;
    }

    public override void PreDraw()
    {
        Stopwatch.Restart();

        base.PreDraw();
    }

    public override void PostDraw()
    {
        Stopwatch.Stop();
        FrameTime = Stopwatch.Elapsed;

        base.PostDraw();
    }

    public override void Draw()
    {
        ImGui.BeginTable("simulatorWindow", 2, ImGuiTableFlags.Resizable);

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 300);
        ImGui.TableNextColumn();
        DrawActions();

        ImGui.TableNextColumn();
        DrawSimulationInfo();

        ImGui.EndTable();

        ImGui.TextUnformatted($"{FrameTime.TotalMilliseconds:0.00}ms");
    }

    private void DrawActions()
    {
        ImGui.BeginChild("CraftimizerActions", Vector2.Zero, true, ImGuiWindowFlags.NoDecoration);
        //ImGui.Checkbox("Show only guaranteed actions", ref showOnlyGuaranteedActions);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        var actionSize = new Vector2(ImGui.GetFontSize() * 2);
        foreach (var (category, actions) in SortedActions)
        {
            var i = 0;
            ImGuiUtils.BeginGroupPanel(category.GetDisplayName());
            foreach (var action in actions)
            {
                var baseAction = action.Base();

                var cannotUse = action.Level() > Input.Stats.Level || (action == ActionType.Manipulation && !Input.Stats.CanUseManipulation);
                var shouldNotUse = !baseAction.CanUse(Simulator) || Simulator.IsComplete;

                ImGui.BeginDisabled(cannotUse);

                if (shouldNotUse)
                    ImGui.PushStyleColor(ImGuiCol.Button, BadActionImageColor);

                if (ImGui.ImageButton(action.GetIcon(ClassJob).ImGuiHandle, actionSize, Vector2.Zero, Vector2.One, -1, default, shouldNotUse ? BadActionImageTint : Vector4.One))
                    AppendAction(action);

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip($"{action.GetName(ClassJob)}\n{baseAction.GetTooltip(Simulator, true)}");

                if (shouldNotUse)
                    ImGui.PopStyleColor();

                ImGui.EndDisabled();

                if (++i % 5 != 0)
                    ImGui.SameLine();
            }
            ImGuiUtils.EndGroupPanel();
        }
        ImGui.PopStyleVar();
        ImGui.EndChild();
    }

    private void DrawSimulationInfo()
    {
        ImGui.BeginChild("simulationInfo", Vector2.Zero, true, ImGuiWindowFlags.NoDecoration);
        DrawSimulationSynth();
        ImGuiHelpers.ScaledDummy(5);
        DrawSimulationEffects();
        ImGuiHelpers.ScaledDummy(5);
        DrawSimulationActions();
        ImGui.EndChild();
    }

    private void DrawSimulationSynth()
    {
        var state = LatestState;
        var imageSize = new Vector2(ImGui.GetFontSize() * 2f);

        ImGui.Image(Icons.GetIconFromId(Item.Icon).ImGuiHandle, imageSize);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.SetCursorPosY(ImGui.GetFontSize()*.75f);
        ImGui.TextUnformatted(Item.Name.ToDalamudString().ToString());
        var availWidth = ImGui.GetContentRegionAvail().X;
        var text = $"Step {state.StepCount + 1}";
        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SameLine(availWidth - textWidth);
        ImGui.AlignTextToFramePadding();
        ImGui.SetCursorPosY(ImGui.GetFontSize() * .75f);
        ImGui.TextUnformatted(text);
        ImGui.Separator();

        ImGui.BeginTable("simSynth", 2);

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableNextColumn();
        ImGuiUtils.BeginGroupPanel("Durability");
        ImGui.TextUnformatted($"{state.Durability} / {Input.Recipe.MaxDurability}");
        DrawProgressBar(state.Durability, Input.Recipe.MaxDurability, new(100, 20), CPColorNew);
        ImGuiUtils.EndGroupPanel();

        ImGuiUtils.BeginGroupPanel("Condition");
        ImGui.TextUnformatted(state.Condition.Name());
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(state.Condition.Description(state.Input.Stats.HasSplendorousBuff));
        ImGuiUtils.EndGroupPanel();

        ImGui.TableNextColumn();

        ImGuiUtils.BeginGroupPanel("Progress");
        DrawProgressBar(state.Progress, Input.Recipe.MaxProgress, new(200, 20), ProgressColor);
        availWidth = ImGui.GetContentRegionAvail().X;
        text = $"{state.Progress} / {Input.Recipe.MaxProgress}";
        textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SameLine(availWidth - textWidth - 10);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f));
        ImGui.TextUnformatted(text);
        ImGuiUtils.EndGroupPanel();

        ImGuiUtils.BeginGroupPanel("Quality");
        DrawProgressBar(state.Quality, Input.Recipe.MaxQuality, new(200, 20), QualityColor);
        availWidth = ImGui.GetContentRegionAvail().X;
        text = $"{state.Quality} / {Input.Recipe.MaxQuality}";
        textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SameLine(availWidth - textWidth - 10);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f));
        ImGui.TextUnformatted(text);
        ImGuiUtils.EndGroupPanel();

        ImGuiUtils.BeginGroupPanel("CP");
        DrawProgressBar(state.CP, Input.Stats.CP, new(200, 20), CPColor);
        availWidth = ImGui.GetContentRegionAvail().X;
        text = $"{state.CP} / {Input.Stats.CP}";
        textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SameLine(availWidth - textWidth - 10);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f));
        ImGui.TextUnformatted(text);
        ImGuiUtils.EndGroupPanel();

        ImGui.Separator();
        ImGui.TextUnformatted($"HQ {state.HQPercent}%");

        ImGui.EndTable();
    }

    private void DrawSimulationSynthOld()
    {
        var state = LatestState;

        ImGui.Text($"Step {state.StepCount + 1}");
        ImGui.Text(state.Condition.Name());
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(state.Condition.Description(state.Input.Stats.HasSplendorousBuff));
        ImGui.Text($"{state.HQPercent}%% HQ");
        DrawProgressBarOld(state.Progress, Input.Recipe.MaxProgress, ProgressColor);
        DrawProgressBarOld(state.Quality, Input.Recipe.MaxQuality, QualityColor);
        DrawProgressBarOld(state.Durability, Input.Recipe.MaxDurability, DurabilityColor);
        DrawProgressBarOld(state.CP, Input.Stats.CP, CPColor);
    }

    private void DrawSimulationEffects()
    {
        ImGui.Text($"Effects:");
        
        var effectHeight = ImGui.GetFontSize() * 2f;
        Vector2 GetEffectSize(TextureWrap icon) => new(icon.Width * effectHeight / icon.Height, effectHeight);

        foreach (var effect in Enum.GetValues<EffectType>())
        {
            var duration = Simulator.GetEffectDuration(effect);
            if (duration == 0)
                continue;

            var strength = Simulator.GetEffectStrength(effect);
            var icon = effect.GetIcon(strength);

            ImGui.Image(icon.ImGuiHandle, GetEffectSize(icon));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(effect.GetTooltip(strength, duration));
            ImGui.SameLine();
        }
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawSimulationActions()
    {
        ImGui.Text($"Actions:");

        var actionSize = new Vector2(ImGui.GetFontSize() * 2f);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        for (var i = 0; i < Actions.Count; ++i)
        {
            var (action, tooltip, response, state) = Actions[i];
            if (ImGui.ImageButton(action.GetIcon(ClassJob).ImGuiHandle, actionSize, Vector2.Zero, Vector2.One, 0, default, response != ActionResponse.UsedAction ? BadActionImageTint : Vector4.One))
                RemoveAction(i);
            if (ImGui.BeginDragDropSource())
            {
                unsafe { ImGui.SetDragDropPayload("simulationAction", (nint)(void*)&i, sizeof(int)); }
                ImGui.ImageButton(action.GetIcon(ClassJob).ImGuiHandle, actionSize);
                ImGui.EndDragDropSource();
            }
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("simulationAction");
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        int droppedIdx;
                        droppedIdx = *(int*)payload.Data;
                        var droppedAction = Actions[droppedIdx].Action;
                        RemoveAction(droppedIdx);
                        InsertAction(i, droppedAction);
                    }
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                var responseText = response switch
                {
                    ActionResponse.SimulationComplete => "Recipe Complete",
                    ActionResponse.ActionNotUnlocked => "Action Not Unlocked",
                    ActionResponse.NotEnoughCP => "Not Enough CP",
                    ActionResponse.NoDurability => "No More Durability",
                    ActionResponse.CannotUseAction => "Cannot Use",
                    _ => string.Empty,
                };
                if (response != ActionResponse.UsedAction)
                    ImGui.TextColored(BadActionTextColor, responseText);
                ImGui.Text($"{action.GetName(ClassJob)}\n{tooltip}");
                DrawProgressBarTooltip(state.Progress, Input.Recipe.MaxProgress, ProgressColor);
                DrawProgressBarTooltip(state.Quality, Input.Recipe.MaxQuality, QualityColor);
                DrawProgressBarTooltip(state.Durability, Input.Recipe.MaxDurability, DurabilityColor);
                DrawProgressBarTooltip(state.CP, Input.Stats.CP, CPColor);
                ImGui.Text("Right Click to Remove\nDrag to Move");
                ImGui.EndTooltip();
            }
            ImGui.SameLine();
        }
        ImGui.PopStyleColor(3);
    }

    private void AppendAction(ActionType action)
    {
        var tooltip = action.Base().GetTooltip(Simulator, false);
        var (response, state) = Simulator.Execute(LatestState, action);
        Actions.Add((action, tooltip, response, state));
    }

    private void RemoveAction(int actionIndex)
    {
        // Remove action
        Actions.RemoveAt(actionIndex);

        // Take note of all actions afterwards
        Span<ActionType> succeedingActions = stackalloc ActionType[Actions.Count - actionIndex];
        for (var i = 0; i < succeedingActions.Length; i++)
            succeedingActions[i] = Actions[i + actionIndex].Action;

        // Remove all future actions
        Actions.RemoveRange(actionIndex, succeedingActions.Length);

        // Re-execute all future actions
        foreach (var action in succeedingActions)
            AppendAction(action);
    }

    private void InsertAction(int actionIndex, ActionType action)
    {
        // Take note of all actions afterwards
        Span<ActionType> succeedingActions = stackalloc ActionType[Actions.Count - actionIndex];
        for (var i = 0; i < succeedingActions.Length; i++)
            succeedingActions[i] = Actions[i + actionIndex].Action;

        // Remove all future actions
        Actions.RemoveRange(actionIndex, succeedingActions.Length);

        // Execute new action
        AppendAction(action);

        // Re-execute all future actions
        foreach (var succeededAction in succeedingActions)
            AppendAction(succeededAction);
    }

    private static void DrawProgressBarTooltip(int progress, int maxProgress, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(Math.Clamp((float)progress / maxProgress, 0f, 1f), TooltipProgressBarSize);
        ImGui.PopStyleColor();
    }

    private static void DrawProgressBarOld(int progress, int maxProgress, Vector4 color) =>
        DrawProgressBar(progress, maxProgress, ProgressBarSize, color, $"{progress} / {maxProgress}");

    private static void DrawProgressBar(int progress, int maxProgress, Vector2 size, Vector4 color, string overlay = "")
    {
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(Math.Clamp((float)progress / maxProgress, 0f, 1f), size, overlay);
        ImGui.PopStyleColor();
    }
}
