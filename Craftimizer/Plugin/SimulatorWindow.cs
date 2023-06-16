using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace Craftimizer.Plugin;

public class SimulatorWindow : Window
{
    public SimulationState Simulation { get; }

    private bool showOnlyGuaranteedActions = true;

    public SimulatorWindow() : base("Craftimizer")
    {
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Simulation = new(new()
        {
            Stats = new CharacterStats { Craftsmanship = 4041, Control = 3905, CP = 609, Level = 90 },
            Recipe = LuminaSheets.RecipeSheet.GetRow(35499)!
        });
    }

    public override void Draw()
    {
        ImGui.BeginTable("CraftimizerTable", 2, ImGuiTableFlags.Resizable);
        ImGui.TableSetupColumn("CraftimizerActionsColumn", ImGuiTableColumnFlags.WidthFixed, 300);
        ImGui.TableNextColumn();
        ImGui.BeginChild("CraftimizerActions", Vector2.Zero, true, ImGuiWindowFlags.NoDecoration);
        ImGui.Checkbox("Show only guaranteed actions", ref showOnlyGuaranteedActions);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        foreach(var category in Enum.GetValues<ActionType>().GroupBy(a => a.Category()))
        {
            var i = 0;
            ImGuiUtils.BeginGroupPanel(category.Key.GetDisplayName());
            foreach (var action in category.OrderBy(a => a.Level()))
            {
                var baseAction = action.With(Simulation);
                if (showOnlyGuaranteedActions && !baseAction.IsGuaranteedAction)
                    continue;

                ImGui.BeginDisabled(!baseAction.CanUse);
                if (ImGui.ImageButton(action.GetIcon(ClassJob.Carpenter).ImGuiHandle, new Vector2(ImGui.GetFontSize() * 2)))
                    Simulation.Execute(action);
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip($"{action.GetName(ClassJob.Carpenter)}\n{baseAction.GetTooltip(true)}");
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
        ImGui.Text($"Step {Simulation.StepCount + 1}");
        ImGui.Text(Simulation.Condition.Name());
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Simulation.Condition.Description(Simulation.Input.Stats.HasRelic));
        ImGui.Text($"{Simulation.HQPercent}%% HQ");
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.2f, 1f, .2f, 1f));
        ImGui.ProgressBar(Math.Min((float)Simulation.Progress / Simulation.Input.MaxProgress, 1f), new Vector2(200, 20), $"{Simulation.Progress} / {Simulation.Input.MaxProgress}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.2f, .2f, 1f, 1f));
        ImGui.ProgressBar(Math.Min((float)Simulation.Quality / Simulation.Input.MaxQuality, 1f), new Vector2(200, 20), $"{Simulation.Quality} / {Simulation.Input.MaxQuality}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(1f, 1f, .2f, 1f));
        ImGui.ProgressBar(Math.Clamp((float)Simulation.Durability / Simulation.Input.MaxDurability, 0f, 1f), new Vector2(200, 20), $"{Simulation.Durability} / {Simulation.Input.MaxDurability}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(1f, .2f, 1f, 1f));
        ImGui.ProgressBar(Math.Clamp((float)Simulation.CP / Simulation.Input.Stats.CP, 0f, 1f), new Vector2(200, 20), $"{Simulation.CP} / {Simulation.Input.Stats.CP}");
        ImGui.PopStyleColor();
        ImGuiHelpers.ScaledDummy(5);
        ImGui.Text($"Effects:");
        foreach (var effect in Simulation.ActiveEffects)
        {
            var icon = effect.Icon;
            var h = ImGui.GetFontSize() * 1.25f;
            var w = icon.Width * h / icon.Height;
            ImGui.Image(icon.ImGuiHandle, new Vector2(w, h));
            ImGui.SameLine();
            ImGui.Text(effect.Tooltip);
        }
        ImGuiHelpers.ScaledDummy(5);
        {
            var i = 0;
            foreach (var action in Simulation.ActionHistory)
            {
                var baseAction = action.With(Simulation);
                ImGui.Image(action.GetIcon(ClassJob.Carpenter).ImGuiHandle, new Vector2(ImGui.GetFontSize() * 2f));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{action.GetName(ClassJob.Carpenter)}\n{baseAction.GetTooltip(false)}");
                if (++i % 5 != 0)
                    ImGui.SameLine();
            }
        }
        ImGui.EndChild();
        ImGui.EndTable();
    }
}
