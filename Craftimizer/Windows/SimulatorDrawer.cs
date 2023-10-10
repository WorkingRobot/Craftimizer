using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace Craftimizer.Plugin.Windows;

public sealed partial class Simulator : Window, IDisposable
{
    private const int ActionColumnSize = 260;

    private static readonly Vector2 ProgressBarSize = new(200, 20);
    private static readonly Vector2 DurabilityBarSize = new(100, 20);
    private static readonly Vector2 ConditionBarSize = new(20, 20);
    private static readonly Vector2 ProgressBarSizeOld = new(200, 20);
    public static readonly Vector2 TooltipProgressBarSize = new(100, 5);

    private static readonly Vector4 ProgressColor = new(0.44f, 0.65f, 0.18f, 1f);
    private static readonly Vector4 QualityColor = new(0.26f, 0.71f, 0.69f, 1f);
    private static readonly Vector4 DurabilityColor = new(0.13f, 0.52f, 0.93f, 1f);
    private static readonly Vector4 HQColor = new(0.592f, 0.863f, 0.376f, 1f);
    private static readonly Vector4 CPColor = new(0.63f, 0.37f, 0.75f, 1f);

    private static readonly Vector4 BadActionImageTint = new(1f, .5f, .5f, 1f);
    private static readonly Vector4 BadActionImageColor = new(1f, .3f, .3f, 1f);

    private static readonly Vector4 BadActionTextColor = new(1f, .2f, .2f, 1f);

    private static readonly (ActionCategory Category, ActionType[] Actions)[] SortedActions;

    static Simulator()
    {
        SortedActions = Enum.GetValues<ActionType>()
            .Where(a => a.Category() != ActionCategory.Combo)
            .GroupBy(a => a.Category())
            .Select(g => (g.Key, g.OrderBy(a => a.Level()).ToArray()))
            .ToArray();
    }

    public override void Draw()
    {
        while (SolverActionQueue.TryDequeue(out var poppedAction))
            AppendGeneratedAction(poppedAction);

        ImGui.BeginTable("simulatorWindow", 2, ImGuiTableFlags.BordersInnerV);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, ActionColumnSize);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextColumn();
        DrawActions();
        ImGui.TableNextColumn();
        DrawSimulation();
        ImGui.EndTable();
    }

    private void DrawActions()
    {
        var hideUnlearnedActions = Config.HideUnlearnedActions;
        if (ImGui.Checkbox("Show only learned actions", ref hideUnlearnedActions))
        {
            Config.HideUnlearnedActions = hideUnlearnedActions;
            Config.Save();
        }

        Sim.SetState(LatestState);

        var actionSize = new Vector2((ActionColumnSize / 5) - ImGui.GetStyle().ItemSpacing.X * (6f / 5));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.BeginDisabled(!CanModifyActions);

        foreach (var (category, actions) in SortedActions)
        {
            var i = 0;
            ImGuiUtils.BeginGroupPanel(category.GetDisplayName(), ActionColumnSize);
            foreach (var action in actions)
            {
                var baseAction = action.Base();

                var cannotUse = action.Level() > Input.Stats.Level || (action == ActionType.Manipulation && !Input.Stats.CanUseManipulation);
                if (cannotUse && Config.HideUnlearnedActions)
                    continue;

                var shouldNotUse = !baseAction.CanUse(Sim) || Sim.IsComplete;

                ImGui.BeginDisabled(cannotUse);

                if (ImGui.ImageButton(action.GetIcon(ClassJob).ImGuiHandle, actionSize, Vector2.Zero, Vector2.One, 0, default, shouldNotUse ? BadActionImageTint : Vector4.One))
                    AppendAction(action);

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip($"{action.GetName(ClassJob)}\n{baseAction.GetTooltip(Sim, true)}");

                ImGui.EndDisabled();

                if (++i % 5 != 0)
                    ImGui.SameLine();
            }
            if (i == 0)
                ImGui.Dummy(actionSize);
            ImGuiUtils.EndGroupPanel();
        }

        ImGui.EndDisabled();
        ImGui.PopStyleColor(3);
    }

    private void DrawSimulation()
    {
        var drawParams = CalculateSynthDrawParams();

        DrawSimulationHeader();
        DrawSimulationBars(drawParams);
        ImGuiHelpers.ScaledDummy(5);
        DrawSimulationEffects(drawParams);
        ImGuiHelpers.ScaledDummy(5);
        DrawSimulationActions(drawParams);
        var bottom = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().FramePadding.Y * 2;
        var buttonHeight = ImGui.GetFrameHeightWithSpacing() * 2 + ImGui.GetFrameHeight();
        ImGuiHelpers.ScaledDummy(bottom - buttonHeight);
        DrawSimulationButtons(drawParams);
    }

    private void DrawSimulationHeader()
    {
        var imageSize = new Vector2(ImGui.GetFontSize() * 2.25f);

        ImGui.Image(Service.IconManager.GetIcon(Item.Icon).ImGuiHandle, imageSize);
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (imageSize.Y - ImGui.GetFontSize()) / 2f);
        ImGui.TextUnformatted(Item.Name.ToDalamudString().ToString());
        if (Item.IsCollectable)
        {
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (imageSize.Y - ImGui.GetFontSize()) / 2f);
            ImGui.TextColored(new(0.98f, 0.98f, 0.61f, 1), SeIconChar.Collectible.ToIconString());
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Collectable");
        }
        if (IsExpert)
        {
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (imageSize.Y - ImGui.GetFontSize()) / 2f);
            // Using ItemLevel icon instead of '◈' because the game fonts hate
            // me and I can't bother to include a font just for this one icon.
            ImGui.TextColored(new(0.93f, 0.59f, 0.45f, 1), SeIconChar.ItemLevel.ToIconString());
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Expert Recipe");
        }
        var availWidth = ImGui.GetContentRegionAvail().X;
        var text = $"Step {LatestState.StepCount + 1}";
        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SameLine(availWidth - textWidth);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (imageSize.Y - ImGui.GetFontSize()) / 2f);
        ImGui.TextUnformatted(text);
        ImGui.Separator();
    }

    private void DrawSimulationBars(SynthDrawParams drawParams)
    {
        var state = LatestState;

        var (leftColumn, rightColumn, leftText, rightText) = (drawParams.LeftColumn, drawParams.RightColumn, drawParams.LeftText, drawParams.RightText);

        ImGui.BeginTable("simSynth", 2);

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, leftColumn);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, rightColumn);
        ImGui.TableNextColumn();

        DrawSynthProgress("Durability", state.Durability, Input.Recipe.MaxDurability, DurabilityBarSize, DurabilityColor, leftText);

        DrawSynthCircle("Condition", state.Condition.Name(), ConditionBarSize, new Vector4(.35f, .35f, .35f, 0) + state.Condition.GetColor(DateTime.UtcNow.TimeOfDay), DurabilityBarSize, leftText);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(state.Condition.Description(state.Input.Stats.HasSplendorousBuff));

        if (Item.IsCollectable)
        {
            var collectibility = Math.Max(state.Quality / 10, 1);
            DrawSynthBar("Collectability", collectibility, Input.Recipe.MaxQuality / 10, $"{collectibility}", DurabilityBarSize, HQColor, leftText);
        }
        else
            DrawSynthBar("HQ %", state.HQPercent, 100, $"{state.HQPercent}%", DurabilityBarSize, HQColor, leftText);

        ImGui.TableNextColumn();

        DrawSynthProgress("Progress", state.Progress, Input.Recipe.MaxProgress, ProgressBarSize, ProgressColor, rightText);
        DrawSynthProgress("Quality", state.Quality, Input.Recipe.MaxQuality, ProgressBarSize, QualityColor, rightText);
        DrawSynthProgress("CP", state.CP, Input.Stats.CP, ProgressBarSize, CPColor, rightText);

        ImGui.EndTable();
    }

    private void DrawSimulationEffects(SynthDrawParams drawParams)
    {
        ImGuiUtils.BeginGroupPanel("Effects", drawParams.Total);

        var effectHeight = ImGui.GetFontSize() * 2f;
        Vector2 GetEffectSize(IDalamudTextureWrap icon) => new(icon.Width * effectHeight / icon.Height, effectHeight);

        ImGui.Dummy(new(0, effectHeight));
        ImGui.SameLine(0, 0);
        foreach (var effect in Enum.GetValues<EffectType>())
        {
            var duration = Sim.GetEffectDuration(effect);
            if (duration == 0)
                continue;

            var strength = Sim.GetEffectStrength(effect);
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

    private void DrawSimulationActions(SynthDrawParams drawParams)
    {
        ImGuiUtils.BeginGroupPanel("Actions", drawParams.Total);

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        var actionSize = new Vector2((drawParams.Total / 10) - ImGui.GetStyle().ItemSpacing.X * (11f / 10));
        ImGui.Dummy(new(0, actionSize.Y));
        ImGui.SameLine(0, 0);
        for (var i = 0; i < Actions.Count; ++i)
        {
            var (action, tooltip, response, state) = Actions[i];
            ImGui.PushID(i);
            if (ImGui.ImageButton(action.GetIcon(ClassJob).ImGuiHandle, actionSize, Vector2.Zero, Vector2.One, 0, default, response != ActionResponse.UsedAction ? BadActionImageTint : Vector4.One))
                if (CanModifyActions)
                    RemoveAction(i);
            if (CanModifyActions)
            {
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
                DrawAllProgressTooltips(state);
                if (CanModifyActions)
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

    private void DrawSimulationButtons(SynthDrawParams drawParams)
    {
        var totalWidth = drawParams.Total;
        var halfWidth = (totalWidth - ImGui.GetStyle().ItemSpacing.X) / 2f;
        var quarterWidth = (halfWidth - ImGui.GetStyle().ItemSpacing.X) / 2f;
        var halfButtonSize = new Vector2(halfWidth, ImGui.GetFrameHeight());
        var quarterButtonSize = new Vector2(quarterWidth, ImGui.GetFrameHeight());

        var conditionRandomnessText = "Condition Randomness";
        var conditionRandomness = Config.ConditionRandomness;
        ImGui.BeginDisabled(!CanModifyActions);
        if (ImGui.Checkbox(conditionRandomnessText, ref conditionRandomness))
        {
            Config.ConditionRandomness = conditionRandomness;
            Config.Save();
            ResetSimulator();
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Allows the condition to fluctuate randomly like a real craft.\nTurns off when generating a macro.");

        var labelSize = ImGui.CalcTextSize(conditionRandomnessText);
        var checkboxWidth = ImGui.GetFrameHeight() + (labelSize.X > 0 ? ImGui.GetStyle().ItemInnerSpacing.X + labelSize.X : 0);
        ImGui.PushFont(UiBuilder.IconFont);
        var cogWidth = ImGui.CalcTextSize(FontAwesomeIcon.Cog.ToIconString()).X;
        ImGui.PopFont();
        ImGui.SameLine(0, totalWidth - ImGui.GetStyle().ItemSpacing.X - checkboxWidth - cogWidth);
        if (ImGuiComponents.IconButton("simSettingsButton", FontAwesomeIcon.Cog))
            Service.Plugin.OpenSettingsTab(Settings.TabSimulator);

        //

        var macroName = MacroName;
        ImGui.SetNextItemWidth(halfWidth);
        if (ImGui.InputTextWithHint("", "Macro Name", ref macroName, 64))
            MacroName = macroName;

        ImGui.SameLine();

        DrawSimulationGenerateButton(halfButtonSize);

        //

        ImGui.BeginDisabled(!CanModifyActions);
        if (Macro != null)
        {
            if (ImGui.Button("Save", quarterButtonSize))
            {
                Macro.Name = MacroName;
                Macro.Actions = Actions.Select(a => a.Action).ToList();
                Config.Save();
            }
            ImGui.SameLine();
        }
        if (ImGui.Button("Save New", Macro == null ? halfButtonSize : quarterButtonSize))
        {
            Macro = new() { Name = MacroName, Actions = Actions.Select(a => a.Action).ToList() };
            Config.Macros.Add(Macro);
            Config.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset", halfButtonSize))
            ClearAllActions();
        ImGui.EndDisabled();
    }

    private void DrawSimulationGenerateButton(Vector2 buttonSize)
    {
        var state = GenerateSolverState();
        string buttonText;
        string tooltipText;
        bool isEnabled;
        var taskCompleted = SolverTask?.IsCompleted ?? true;
        var taskCancelled = SolverTaskToken?.IsCancellationRequested ?? false;
        if (!taskCompleted)
        {
            if (taskCancelled)
            {
                buttonText = "Cancelling...";
                tooltipText = "Cancelling macro generation. This shouldn't take long.";
                isEnabled = false;
            }
            else
            {
                buttonText = "Cancel";
                tooltipText = "Cancel macro generation";
                isEnabled = true;
            }
        }
        else
        {
            if (SolverActionsChanged)
            {
                buttonText = "Generate";
                tooltipText = "Generate a set of actions to finish the macro.";
                isEnabled = state.HasValue;
                if (!isEnabled)
                    tooltipText += "\nMake sure your craft so far is valid (without random condition changes)";
            }
            else
            {
                buttonText = "Regenerate";
                tooltipText = "Retry and regenerate a new set of actions to finish the macro.";
                isEnabled = true;
            }
        }
        ImGui.BeginDisabled(!isEnabled);
        if (ImGui.Button(buttonText, buttonSize))
        {
            if (!taskCompleted)
            {
                if (!taskCancelled)
                    SolverTaskToken?.Cancel();
            }
            else
            {
                if (SolverActionsChanged)
                {
                    if (state.HasValue)
                        SolveMacro(state.Value);
                }
                else
                {
                    Actions.RemoveRange(SolverInitialActionCount, Actions.Count - SolverInitialActionCount);
                    SolveMacro(GenerateSolverState()!.Value);
                }
            }
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tooltipText);
    }
}
