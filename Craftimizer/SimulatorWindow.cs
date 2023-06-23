using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Numerics;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Plugin;

public class SimulatorWindow : Window
{
    public Simulator.Simulator Simulation { get; }
    private SimulationState State { get; set; }

    private bool showOnlyGuaranteedActions = true;

    public SimulatorWindow() : base("Craftimizer")
    {
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        State = new(new(
            new CharacterStats { Craftsmanship = 4041, Control = 3905, CP = 609, Level = 90, CLvl = CalculateCLvl(90) },
            CreateRecipeInfo(LuminaSheets.RecipeSheet.GetRow(35499)!),
            0
        ));
        Simulation = new(State);
    }

    private static RecipeInfo CreateRecipeInfo(Recipe recipe)
    {
        var recipeTable = recipe.RecipeLevelTable.Value!;
        return new() {
            IsExpert = recipe.IsExpert,
            ClassJobLevel = recipeTable.ClassJobLevel,
            RLvl = (int)recipeTable.RowId,
            ConditionsFlag = recipeTable.ConditionsFlag,
            MaxDurability = recipeTable.Durability * recipe.DurabilityFactor / 100,
            MaxQuality = (int)recipeTable.Quality * recipe.QualityFactor / 100,
            MaxProgress = recipeTable.Difficulty * recipe.DifficultyFactor / 100,
            QualityModifier = recipeTable.QualityModifier,
            QualityDivider = recipeTable.QualityDivider,
            ProgressModifier = recipeTable.ProgressModifier,
            ProgressDivider = recipeTable.ProgressDivider,
        };
    }

    private static int CalculateCLvl(int characterLevel) =>
        characterLevel <= 80
        ? LuminaSheets.ParamGrowSheet.GetRow((uint)characterLevel)!.CraftingLevel
        : (int)LuminaSheets.RecipeLevelTableSheet.First(r => r.ClassJobLevel == characterLevel).RowId;

    public override void Draw()
    {
        ImGui.BeginTable("CraftimizerTable", 2, ImGuiTableFlags.Resizable);
        ImGui.TableSetupColumn("CraftimizerActionsColumn", ImGuiTableColumnFlags.WidthFixed, 300);
        ImGui.TableNextColumn();
        ImGui.BeginChild("CraftimizerActions", Vector2.Zero, true, ImGuiWindowFlags.NoDecoration);
        ImGui.Checkbox("Show only guaranteed actions", ref showOnlyGuaranteedActions);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        foreach (var category in Enum.GetValues<ActionType>().GroupBy(a => a.Category()))
        {
            var i = 0;
            ImGuiUtils.BeginGroupPanel(category.Key.GetDisplayName());
            foreach (var action in category.OrderBy(a => a.Level()))
            {
                var baseAction = action.Base();
                if (showOnlyGuaranteedActions && baseAction.SuccessRate(Simulation) != 1)
                    continue;

                ImGui.BeginDisabled(!baseAction.CanUse(Simulation) || Simulation.IsComplete);
                if (ImGui.ImageButton(action.GetIcon(ClassJob.Carpenter).ImGuiHandle, new Vector2(ImGui.GetFontSize() * 2)))
                    (_, State) = Simulation.Execute(State, action);
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip($"{action.GetName(ClassJob.Carpenter)}\n{baseAction.GetTooltip(Simulation, true)}");
                ImGui.EndDisabled();
                if (++i % 5 != 0)
                    ImGui.SameLine();
            }
            ImGuiUtils.EndGroupPanel();
        }
        ImGui.PopStyleVar();
        ImGui.EndChild();
        ImGui.TableNextColumn();
        ImGui.BeginChild("CraftimizerSimulator", Vector2.Zero, true, ImGuiWindowFlags.NoDecoration);
        ImGui.Text($"Step {State.StepCount + 1}");
        ImGui.Text(State.Condition.Name());
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(State.Condition.Description(State.Input.Stats.HasSplendorousBuff));
        ImGui.Text($"{State.HQPercent}%% HQ");
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.2f, 1f, .2f, 1f));
        ImGui.ProgressBar(Math.Min((float)State.Progress / State.Input.Recipe.MaxProgress, 1f), new Vector2(200, 20), $"{State.Progress} / {State.Input.Recipe.MaxProgress}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.2f, .2f, 1f, 1f));
        ImGui.ProgressBar(Math.Min((float)State.Quality / State.Input.Recipe.MaxQuality, 1f), new Vector2(200, 20), $"{State.Quality} / {State.Input.Recipe.MaxQuality}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(1f, 1f, .2f, 1f));
        ImGui.ProgressBar(Math.Clamp((float)State.Durability / State.Input.Recipe.MaxDurability, 0f, 1f), new Vector2(200, 20), $"{State.Durability} / {State.Input.Recipe.MaxDurability}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(1f, .2f, 1f, 1f));
        ImGui.ProgressBar(Math.Clamp((float)State.CP / State.Input.Stats.CP, 0f, 1f), new Vector2(200, 20), $"{State.CP} / {State.Input.Stats.CP}");
        ImGui.PopStyleColor();
        ImGuiHelpers.ScaledDummy(5);
        ImGui.Text($"Effects:");
        foreach (var effect in Enum.GetValues<EffectType>())
        {
            var strength = Simulation.GetEffectStrength(effect);
            var duration = Simulation.GetEffectDuration(effect);
            var icon = effect.GetIcon(strength);
            var h = ImGui.GetFontSize() * 1.25f;
            var w = icon.Width * h / icon.Height;
            ImGui.Image(icon.ImGuiHandle, new Vector2(w, h));
            ImGui.SameLine();
            ImGui.Text(effect.GetTooltip(strength, duration));
        }
        ImGuiHelpers.ScaledDummy(5);
        {
            ImGui.Text("TODO: Action History");
        }
        ImGui.EndChild();
        ImGui.EndTable();
    }
}
