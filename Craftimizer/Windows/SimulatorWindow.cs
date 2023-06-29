using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Plugin.Windows;

public sealed partial class SimulatorWindow : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.AlwaysAutoResize;

    private static Configuration Configuration => Service.Configuration;

    private Item Item { get; }
    private bool IsExpert { get; }
    private SimulationInput Input { get; }
    private ClassJob ClassJob { get; }
    private Macro? Macro { get; set; }
    private string MacroName { get; set; }
    // State is the state of the simulation *after* its corresponding action is executed.
    private List<(ActionType Action, string Tooltip, ActionResponse Response, SimulationState State)> Actions { get; }
    private Simulator.Simulator Simulator { get; set; }

    private SimulationState LatestState => Actions.Count == 0 ? new(Input) : Actions[^1].State;

    // Simulator is set by ResetSimulator()
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public SimulatorWindow(Item item, bool isExpert, SimulationInput input, ClassJob classJob, Macro? macro) : base("Simulator", WindowFlags)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        Service.WindowSystem.AddWindow(this);

        Item = item;
        IsExpert = isExpert;
        Input = input;
        ClassJob = classJob;
        Macro = macro;
        MacroName = Macro?.Name ?? $"Macro {Configuration.Macros.Count + 1}";
        Actions = new();
        ResetSimulator();

        IsOpen = true;

        CollapsedCondition = ImGuiCond.Appearing;
        Collapsed = false;

        SizeCondition = ImGuiCond.Appearing;
        Size = SizeConstraints?.MinimumSize ?? new(10);

        if (Macro != null)
            foreach (var action in Macro.Actions)
                AppendAction(action);
    }

    private void ResetSimulator()
    {
        Simulator = Service.Configuration.CreateSimulator(LatestState);
        ReexecuteAllActions();
    }
}
