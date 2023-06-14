using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface;
using Dalamud.Interface.Components;
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

        Simulation = new(new CharacterStats { Craftsmanship = 4041, Control = 905, CP = 609, Level = 90 }, LuminaSheets.RecipeSheet.GetRow(35573)!);
        AvailableActions = BaseAction.Actions.Select(a => (Activator.CreateInstance(a, Simulation)! as BaseAction)!).ToArray();
    }

    public override void Draw()
    {
        ImGui.BeginTable("CraftimizerTable", 2, ImGuiTableFlags.Resizable);
        ImGui.TableSetupColumn("CraftimizerActionsColumn", ImGuiTableColumnFlags.WidthFixed, 300);
        ImGui.TableNextColumn();
        ImGui.BeginChild("CraftimizerActions", Vector2.Zero, true, ImGuiWindowFlags.NoDecoration);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        foreach(var action in AvailableActions)
        {
            ImGui.BeginDisabled(!action.CanUse);
            if (ImGui.ImageButton(action.GetIcon(ClassJob.Carpenter).ImGuiHandle, new Vector2(ImGui.GetFontSize() * 4)))
                Simulation.Execute(action);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(action.GetName(ClassJob.Carpenter));
            ImGui.EndDisabled();
            ImGui.SameLine();
        }
        ImGui.PopStyleVar();
        ImGui.EndChild();
        ImGui.TableNextColumn();
        ImGui.BeginChild("CraftimizerSimulator", Vector2.Zero, true, ImGuiWindowFlags.NoDecoration);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.2f, 1f, .2f, 1f));
        ImGui.ProgressBar(Math.Min((float)Simulation.Progress / Simulation.MaxProgress, 1f), new Vector2(200, 10));
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.2f, .2f, 1f, 1f));
        ImGui.ProgressBar(Math.Min((float)Simulation.Quality / Simulation.MaxQuality, 1f), new Vector2(200, 10));
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, .2f, 1f));
        ImGui.ProgressBar(Math.Clamp((float)Simulation.Durability / Simulation.MaxDurability, 0f, 1f), new Vector2(200, 10));
        ImGui.PopStyleColor();
        ImGuiHelpers.ScaledDummy(5);
        ImGui.Text($"Effects:");
        foreach (var (effect, strength, stepsLeft) in Simulation.ActiveEffects)
            ImGui.Text($"{effect} {strength} - {stepsLeft}");
        ImGui.EndChild();
        ImGui.EndTable();
    }
}
