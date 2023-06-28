using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using ActionCategory = Craftimizer.Simulator.ActionCategory;
using ClassJob = Craftimizer.Simulator.ClassJob;
using Condition = Craftimizer.Simulator.Condition;

namespace Craftimizer.Plugin.Windows;

public class SimulatorWindow : Window
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.AlwaysAutoResize;

    private static readonly Vector2 ProgressBarSize = new(200, 20);
    private static readonly Vector2 DurabilityBarSize = new(100, 20);
    private static readonly Vector2 ConditionBarSize = new(20, 20);
    private static readonly Vector2 ProgressBarSizeOld = new(200, 20);
    private static readonly Vector2 TooltipProgressBarSize = new(100, 5);

    private static readonly Vector4 ProgressColor   = new(0.44f, 0.65f, 0.18f, 1f);
    private static readonly Vector4 QualityColor    = new(0.26f, 0.71f, 0.69f, 1f);
    private static readonly Vector4 DurabilityColor = new(0.13f, 0.52f, 0.93f, 1f);
    private static readonly Vector4 HQColor         = new(0.592f, 0.863f, 0.376f, 1f);
    private static readonly Vector4 CPColor         = new(0.63f, 0.37f, 0.75f, 1f);

    private static readonly Vector4 BadActionImageTint = new(1f, .5f, .5f, 1f);
    private static readonly Vector4 BadActionImageColor = new(1f, .3f, .3f, 1f);

    private static readonly Vector4 BadActionTextColor = new(1f, .2f, .2f, 1f);

    private static readonly (ActionCategory Category, ActionType[] Actions)[] SortedActions;

    private TimeSpan FrameTime { get; set; }
    private Stopwatch Stopwatch { get; } = new();

    private Item Item { get; }
    private bool IsExpert { get; }
    private SimulationInput Input { get; }
    private ClassJob ClassJob { get; }
    // State is the state of the simulation *after* its corresponding action is executed.
    private List<(ActionType Action, string Tooltip, ActionResponse Response, SimulationState State)> Actions { get; }
    private Simulator.Simulator Simulator { get; }

    private SimulationState LatestState => Actions.Count == 0 ? new(Input) : Actions[^1].State;

    static SimulatorWindow()
    {
        SortedActions = Enum.GetValues<ActionType>().GroupBy(a => a.Category()).Select(g => (g.Key, g.OrderBy(a => a.Level()).ToArray())).ToArray();
    }

    public SimulatorWindow(Item item, bool isExpert, SimulationInput input, ClassJob classJob, List<ActionType> actions) : base("Simulator", WindowFlags)
    {
        Service.WindowSystem.AddWindow(this);

        Item = item;
        IsExpert = isExpert;
        Input = input;
        ClassJob = classJob;
        Actions = new();
        Simulator = Service.Configuration.CreateSimulator(new(input));

        foreach(var action in actions)
            AppendAction(action);

        IsOpen = true;
        CollapsedCondition = ImGuiCond.Appearing;
        Collapsed = false;
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

        /*
        unsafe {
            var unitBase = (AtkUnitBase*)Service.GameGui.GetAddonByName("Synthesis");
            if (unitBase != null)
            {
                var res = unitBase->GetNodeById(95)->GetAsAtkComponentNode()->Component;
                var cond = MemoryHelper.ReadStringNullTerminated((nint)res->GetTextNodeById(4)->GetAsAtkTextNode()->GetText());
                var img = res->GetImageNodeById(3);

                var d = unchecked(((short)img->AddRed, (short)img->AddGreen, (short)img->AddBlue));
                PluginLog.LogDebug($"{cond} -> {d}");
            }
        }
        */
        base.PostDraw();
    }

    public override void Draw()
    {
        ImGui.BeginTable("simulatorWindow", 2, ImGuiTableFlags.BordersInnerV);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 260);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextColumn();
        DrawActions();
        ImGui.TableNextColumn();
        DrawSimulationInfo();
        ImGui.EndTable();

        ImGui.TextUnformatted($"{FrameTime.TotalMilliseconds:0.00}ms");
        return;
    }
    
    private void DrawActions()
    {

        var actionSize = new Vector2(ImGui.GetFontSize() * 2f);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);

        //ImGui.Checkbox("Show only guaranteed actions", ref showOnlyGuaranteedActions);

        foreach (var (category, actions) in SortedActions)
        {
            var i = 0;
            ImGuiUtils.BeginGroupPanel(category.GetDisplayName(), 260);
            foreach (var action in actions)
            {
                var baseAction = action.Base();

                var cannotUse = action.Level() > Input.Stats.Level || (action == ActionType.Manipulation && !Input.Stats.CanUseManipulation);
                if (cannotUse && Service.Configuration.HideUnlearnedActions)
                    continue;

                var shouldNotUse = !baseAction.CanUse(Simulator) || Simulator.IsComplete;

                ImGui.BeginDisabled(cannotUse);

                if (ImGui.ImageButton(action.GetIcon(ClassJob).ImGuiHandle, actionSize, Vector2.Zero, Vector2.One, 0, default, shouldNotUse ? BadActionImageTint : Vector4.One))
                    AppendAction(action);

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip($"{action.GetName(ClassJob)}\n{baseAction.GetTooltip(Simulator, true)}");

                ImGui.EndDisabled();

                if (++i % 5 != 0)
                    ImGui.SameLine();
            }
            if (i == 0)
                ImGui.Dummy(actionSize);
            ImGuiUtils.EndGroupPanel();
        }
        ImGui.PopStyleColor(3);
    }

    private void DrawSimulationInfo()
    {
        DrawSimulationSynth();
        ImGuiHelpers.ScaledDummy(5);
        DrawSimulationEffects();
        ImGuiHelpers.ScaledDummy(5);
        DrawSimulationActions();
    }

    private (float leftColumn, float leftText, float rightColumn, float rightText, float totalWidth) CalculateSimulationSynthWidths()
    {
        var sidePadding = ImGui.GetFrameHeight() / 2;
        var separatorTextWidth = ImGui.CalcTextSize(" / ").X;
        var itemSpacing = ImGui.GetStyle().ItemSpacing.X * 2;

        var leftDigits = (int)MathF.Floor(MathF.Log10(Input.Recipe.MaxDurability) + 1);
        var leftTextWidth = ImGui.CalcTextSize(new string('0', leftDigits)).X;
        var leftWidth = DurabilityBarSize.X + sidePadding + itemSpacing + separatorTextWidth + leftTextWidth * 2;


        var rightDigits = (int)MathF.Floor(MathF.Log10(Math.Max(Math.Max(Input.Recipe.MaxProgress, Input.Recipe.MaxQuality), Input.Stats.CP)) + 1);
        var rightTextWidth = ImGui.CalcTextSize(new string('0', rightDigits)).X;
        var rightWidth = ProgressBarSize.X + sidePadding + itemSpacing + separatorTextWidth + rightTextWidth * 2;

        return (leftWidth, leftTextWidth, rightWidth, rightTextWidth, leftWidth + rightWidth + itemSpacing / 2);
    }

    private void DrawSimulationSynth()
    {
        var state = LatestState;
        var imageSize = new Vector2(ImGui.GetFontSize() * 2f);

        {
            ImGui.Image(Icons.GetIconFromId(Item.Icon).ImGuiHandle, imageSize);
            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetFontSize() * .5f);
            ImGui.TextUnformatted(Item.Name.ToDalamudString().ToString());
            if (Item.IsCollectable)
            {
                ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetFontSize() * .5f);
                ImGui.TextColored(new(0.98f, 0.98f, 0.61f, 1), "(Collectible)");
            }
            if (IsExpert)
            {
                ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetFontSize() * .5f);
                ImGui.TextColored(new(0.941f, 0.557f, 0.216f, 1), "(Expert)");
            }
            var availWidth = ImGui.GetContentRegionAvail().X;
            var text = $"Step {state.StepCount + 1}";
            var textWidth = ImGui.CalcTextSize(text).X;
            ImGui.SameLine(availWidth - textWidth);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetFontSize() * .5f);
            ImGui.TextUnformatted(text);
            ImGui.Separator();
        }

        ImGui.BeginTable("simSynth", 2);

        var (leftWidth, leftTextWidth, rightWidth, rightTextWidth, _) = CalculateSimulationSynthWidths();

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, leftWidth);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, rightWidth);
        ImGui.TableNextColumn();

        DrawSynthBarCenteredProgress("Durability", state.Durability, Input.Recipe.MaxDurability, DurabilityBarSize, DurabilityColor, leftTextWidth);

        DrawSynthBarCenteredCircle("Condition", state.Condition.Name(), ConditionBarSize, new Vector4(.35f, .35f, .35f, 0) + state.Condition.GetColor(DateTime.UtcNow.TimeOfDay), DurabilityBarSize, leftTextWidth);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(state.Condition.Description(state.Input.Stats.HasSplendorousBuff));

        if (Item.IsCollectable)
        {
            var collectibility = Math.Max(state.Quality / 10, 1);
            DrawSynthBarCentered("Collectability", collectibility, Input.Recipe.MaxQuality / 10, $"{collectibility}", DurabilityBarSize, HQColor, leftTextWidth);
        }
        else
            DrawSynthBarCentered("HQ %", state.HQPercent, 100, $"{state.HQPercent}%", DurabilityBarSize, HQColor, leftTextWidth);

        ImGui.TableNextColumn();

        DrawSynthBarCenteredProgress("Progress", state.Progress, Input.Recipe.MaxProgress, ProgressBarSize, ProgressColor, rightTextWidth);
        DrawSynthBarCenteredProgress("Quality", state.Quality, Input.Recipe.MaxQuality, ProgressBarSize, QualityColor, rightTextWidth);
        DrawSynthBarCenteredProgress("CP", state.CP, Input.Stats.CP, ProgressBarSize, CPColor, rightTextWidth);

        ImGui.EndTable();
    }

    private void DrawSynthBarCenteredProgress(string name, int current, int max, Vector2 size, Vector4 color, float textWidth)
    {
        ImGuiUtils.BeginGroupPanel(name);

        DrawProgressBar(current, max, size, color);

        var w = ImGui.GetStyle().ItemSpacing.X;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));

        ImGui.SameLine(0, textWidth - ImGui.CalcTextSize($"{current}").X + w);
        var adjustedHeight = ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f);
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted($"{current}");

        ImGui.SameLine();
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted(" / ");

        ImGui.SameLine(0, textWidth - ImGui.CalcTextSize($"{max}").X);
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted($"{max}");

        ImGui.PopStyleVar();

        ImGuiUtils.EndGroupPanel();
    }

    private void DrawSynthBarCenteredCircle(string name, string text, Vector2 size, Vector4 color, Vector2 otherProgressSize, float textWidth)
    {
        ImGuiUtils.BeginGroupPanel(name);

        var w = ImGui.GetStyle().ItemSpacing.X;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));

        var contentWidth = size.X + w + ImGui.CalcTextSize(text).X;
        var totalWidth = otherProgressSize.X + w + textWidth * 2 + ImGui.CalcTextSize(" / ").X;

        ImGui.Dummy(default);
        ImGui.SameLine(0, (totalWidth - contentWidth) / 2);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Math.Max(size.X, size.Y));
        DrawProgressBar(1, 1, size, color);
        ImGui.PopStyleVar();
        ImGui.SameLine(0, w);
        var adjustedHeight = ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f);
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted(text);

        ImGui.PopStyleVar();

        ImGuiUtils.EndGroupPanel();
    }

    private void DrawSynthBarCentered(string name, int current, int max, string text, Vector2 size, Vector4 color, float textWidth)
    {
        ImGuiUtils.BeginGroupPanel(name);

        DrawProgressBar(current, max, size, color);

        var w = ImGui.GetStyle().ItemSpacing.X;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));

        var totalWidth = textWidth * 2 + ImGui.CalcTextSize(" / ").X;

        ImGui.SameLine(0, totalWidth - ImGui.CalcTextSize(text).X + w);
        var adjustedHeight = ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f);
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted(text);

        ImGui.PopStyleVar();

        ImGuiUtils.EndGroupPanel();
    }

    private void DrawSimulationEffects()
    {
        var (_, _, _, _, totalWidth) = CalculateSimulationSynthWidths();

        ImGuiUtils.BeginGroupPanel("Effects", totalWidth);

        var effectHeight = ImGui.GetFontSize() * 2f;
        Vector2 GetEffectSize(TextureWrap icon) => new(icon.Width * effectHeight / icon.Height, effectHeight);

        ImGui.Dummy(new(0, effectHeight));
        ImGui.SameLine(0, 0);
        foreach (var effect in Enum.GetValues<EffectType>())
        {
            var duration = Simulator.GetEffectDuration(effect);
            if (duration == 0)
                continue;

            var strength = Simulator.GetEffectStrength(effect);
            var icon = effect.GetIcon(strength);
            var iconSize = GetEffectSize(icon);

            ImGui.Image(icon.ImGuiHandle, iconSize);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(effect.GetTooltip(strength, duration));
            if (duration != 0)
            {
                ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (effectHeight - ImGui.GetFontSize()) / 2f);
                ImGui.Text($"{duration}");
            }
            ImGui.SameLine();
        }
        ImGui.Dummy(Vector2.Zero);

        ImGuiUtils.EndGroupPanel();
    }

    private void DrawSimulationActions()
    {
        var (_, _, _, _, totalWidth) = CalculateSimulationSynthWidths();

        ImGuiUtils.BeginGroupPanel("Actions", totalWidth);

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        var actionSize = new Vector2((totalWidth / 10) - ImGui.GetStyle().ItemSpacing.X * (11f/10));
        ImGui.Dummy(new(0, actionSize.Y));
        ImGui.SameLine(0, 0);
        for (var i = 0; i < Actions.Count; ++i)
        {
            var (action, tooltip, response, state) = Actions[i];
            ImGui.PushID(i);
            if (ImGui.ImageButton(action.GetIcon(ClassJob).ImGuiHandle, actionSize, Vector2.Zero, Vector2.One, 0, default, response != ActionResponse.UsedAction ? BadActionImageTint : Vector4.One))
                RemoveAction(i);
            if (ImGui.BeginDragDropSource())
            {
                unsafe { ImGui.SetDragDropPayload("simulationAction", (nint)(&i), sizeof(int)); }
                ImGui.ImageButton(Actions[i].Action.GetIcon(ClassJob).ImGuiHandle, actionSize);
                ImGui.EndDragDropSource();
            }
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("simulationAction");
                bool isValidPayload;
                unsafe { isValidPayload = payload.NativePtr != null; }
                if (isValidPayload)
                {
                    int draggedIdx;
                    unsafe { draggedIdx = *(int*)payload.Data; }
                    var draggedAction = Actions[draggedIdx].Action;
                    RemoveAction(draggedIdx);
                    InsertAction(i, draggedAction);
                }
                ImGui.EndDragDropTarget();
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
                ImGui.Text("Click to Remove\nDrag to Move");
                ImGui.EndTooltip();
            }
            ImGui.PopID();
            if (i % 10 != 9)
                ImGui.SameLine();
        }
        ImGui.PopStyleColor(3);

        ImGuiUtils.EndGroupPanel();
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

    private static void DrawProgressBarTooltip(int progress, int maxProgress, Vector4 color) =>
        DrawProgressBar(progress, maxProgress, TooltipProgressBarSize, color);

    private static void DrawProgressBar(int progress, int maxProgress, Vector2 size, Vector4 color, string overlay = "")
    {
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(Math.Clamp((float)progress / maxProgress, 0f, 1f), size, overlay);
        ImGui.PopStyleColor();
    }
}
