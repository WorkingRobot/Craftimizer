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
    public Simulation Simulation { get; }
    public BaseAction[] AvailableActions { get; }

    public SimulatorWindow() : base("Craftimizer")
    {
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Simulation = new(new CharacterStats { Craftsmanship = 4041, Control = 3905, CP = 609, Level = 90 }, LuminaSheets.RecipeSheet.GetRow(35573)!);
        AvailableActions = BaseAction.Actions.Select(a => (Activator.CreateInstance(a, Simulation)! as BaseAction)!).ToArray();
    }

    public override void Draw()
    {
        ImGui.BeginTable("CraftimizerTable", 2, ImGuiTableFlags.Resizable);
        ImGui.TableSetupColumn("CraftimizerActionsColumn", ImGuiTableColumnFlags.WidthFixed, 300);
        ImGui.TableNextColumn();
        ImGui.BeginChild("CraftimizerActions", Vector2.Zero, true, ImGuiWindowFlags.NoDecoration);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        foreach(var category in AvailableActions.GroupBy(a=>a.Category))
        {
            var i = 0;
            ImGuiUtils.BeginGroupPanel(category.Key.ToString());
            foreach (var action in category)
            {
                ImGui.BeginDisabled(!action.CanUse);
                if (ImGui.ImageButton(action.GetIcon(ClassJob.Carpenter).ImGuiHandle, new Vector2(ImGui.GetFontSize() * 2)))
                    Simulation.Execute(action);
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(action.Tooltip);
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
        ImGui.Text($"{Simulation.HQPercent}%% HQ");
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.2f, 1f, .2f, 1f));
        ImGui.ProgressBar(Math.Min((float)Simulation.Progress / Simulation.MaxProgress, 1f), new Vector2(200, 20), $"{Simulation.Progress} / {Simulation.MaxProgress}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.2f, .2f, 1f, 1f));
        ImGui.ProgressBar(Math.Min((float)Simulation.Quality / Simulation.MaxQuality, 1f), new Vector2(200, 20), $"{Simulation.Quality} / {Simulation.MaxQuality}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(1f, 1f, .2f, 1f));
        ImGui.ProgressBar(Math.Clamp((float)Simulation.Durability / Simulation.MaxDurability, 0f, 1f), new Vector2(200, 20), $"{Simulation.Durability} / {Simulation.MaxDurability}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(1f, .2f, 1f, 1f));
        ImGui.ProgressBar(Math.Clamp((float)Simulation.CP / Simulation.Stats.CP, 0f, 1f), new Vector2(200, 20), $"{Simulation.CP} / {Simulation.Stats.CP}");
        ImGui.PopStyleColor();
        ImGuiHelpers.ScaledDummy(5);
        ImGui.Text($"Effects:");
        foreach (var (effect, strength, stepsLeft) in Simulation.ActiveEffects)
        {
            var status = effect.Status();
            var icon = Icons.GetIconFromId((ushort)status.Icon);
            var h = ImGui.GetFontSize() * 1.25f;
            var w = icon.Width * h / icon.Height;
            ImGui.Image(icon.ImGuiHandle, new Vector2(w, h));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(status.Name.ToString());
            ImGui.SameLine();
            if (stepsLeft < 0)
                ImGui.Text($"{strength}");
            else
                ImGui.Text($"> {stepsLeft}");
        }
        ImGuiHelpers.ScaledDummy(5);
        {
            var i = 0;
            foreach (var action in Simulation.ActionHistory)
            {
                ImGui.Image(action.GetIcon(ClassJob.Carpenter).ImGuiHandle, new Vector2(ImGui.GetFontSize() * 2f));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(action.GetName(ClassJob.Carpenter));
                if (++i % 5 != 0)
                    ImGui.SameLine();
            }
        }
        ImGui.EndChild();
        ImGui.EndTable();
    }
}
