using Craftimizer.Plugin.Utils;
using Craftimizer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using System;
using System.Numerics;

namespace Craftimizer.Plugin.Windows;

public sealed unsafe partial class Craft : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration
      | ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoSavedSettings
      | ImGuiWindowFlags.NoFocusOnAppearing
      | ImGuiWindowFlags.NoNavFocus;

    private const float WindowWidth = 300;
    private const int ActionsPerRow = 5;
    private static readonly Vector2 CraftProgressBarSize = new(WindowWidth, 15);

    private static Configuration Config => Service.Configuration;

    private static RecipeNote RecipeUtils => Service.Plugin.RecipeNote;

    private bool WasOpen { get; set; }

    public Craft() : base("Craftimizer SynthesisHelper", WindowFlags, true)
    {
        Service.WindowSystem.AddWindow(this);
        Service.Plugin.Hooks.OnActionUsed += OnActionUsed;

        IsOpen = true;
    }

    public override void Draw()
    {
        SolveTick();
        DequeueSolver();

        DrawActions();

        ImGui.SameLine(0, 0);
        ImGui.Dummy(default);

        ImGuiHelpers.ScaledDummy(5);

        Simulator.DrawAllProgressBars(SolverLatestState, CraftProgressBarSize);

        ImGuiHelpers.ScaledDummy(5);

        ImGui.PushFont(UiBuilder.IconFont);
        var cogWidth = ImGui.CalcTextSize(FontAwesomeIcon.Cog.ToIconString()).X + (ImGui.GetStyle().FramePadding.X * 2);
        ImGui.PopFont();

        DrawSolveButton(new(WindowWidth - ImGui.GetStyle().ItemSpacing.X - cogWidth, ImGuiUtils.ButtonHeight));

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("synthSettingsButton", FontAwesomeIcon.Cog))
            Service.Plugin.OpenSettingsTab(Settings.TabSynthHelper);
    }

    private void DrawActions()
    {
        var actionSize = new Vector2((WindowWidth / ActionsPerRow) - (ImGui.GetStyle().ItemSpacing.X * ((ActionsPerRow - 1f) / ActionsPerRow)));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);

        ImGui.Dummy(new(0, actionSize.Y));
        ImGui.SameLine(0, 0);
        for (var i = 0; i < SolverActions.Count; ++i)
        {
            var (action, tooltip, state) = SolverActions[i];
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
            if (i % ActionsPerRow != (ActionsPerRow - 1))
                ImGui.SameLine();
        }

        ImGui.PopStyleColor(3);
    }

    private void DrawSolveButton(Vector2 buttonSize)
    {
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
                tooltipText = "Cancelling action generation. This shouldn't take long.";
                isEnabled = false;
            }
            else
            {
                buttonText = "Cancel";
                tooltipText = "Cancel action generation";
                isEnabled = true;
            }
        }
        else
        {
            buttonText = "Retry";
            tooltipText = "Retry and regenerate a new set of recommended actions to finish the craft.";
            isEnabled = true;
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
                QueueSolve(GetNextState()!.Value);
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tooltipText);
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

        base.PreDraw();
    }

    private bool DrawConditionsInner()
    {
        if (!RecipeUtils.HasValidRecipe)
            return false;

        if (!RecipeUtils.IsCrafting)
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
        if (!Config.EnableSynthHelper)
            return false;

        var ret = DrawConditionsInner();
        if (ret && !WasOpen)
            ResetSimulation();

        WasOpen = ret;
        return ret;
    }

    private void ResetSimulation()
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            return;

        CharacterStats = Gearsets.CalculateCharacterStats(Gearsets.CalculateGearsetCurrentStats(), Gearsets.GetGearsetItems(container), RecipeUtils.CharacterLevel, RecipeUtils.CanUseManipulation);
        Input = new(CharacterStats, RecipeUtils.Info, 0);
        ActionCount = 0;
        ActionStates = new();
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
