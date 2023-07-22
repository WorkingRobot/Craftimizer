using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver.Crafty;
using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Craftimizer.Plugin;

[Serializable]
public class Macro
{
    public string Name { get; set; } = string.Empty;
    public List<ActionType> Actions { get; set; } = new();
}

public static class AlgorithmUtils
{
    public static void Invoke(this SolverConfig me, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
    {
        try
        {
            Solver.Crafty.Solver.Search(me, state, actionCallback, token);
        }
        catch (AggregateException e)
        {
            e.Handle(ex => ex is OperationCanceledException);
        }
        catch (OperationCanceledException)
        {

        }
    }
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool OverrideUncraftability { get; set; } = true;
    public bool HideUnlearnedActions { get; set; } = true;
    public List<Macro> Macros { get; set; } = new();
    public SolverConfig SimulatorSolverConfig { get; set; } = SolverConfig.SimulatorDefault;
    public SolverConfig SynthHelperSolverConfig { get; set; } = SolverConfig.SynthHelperDefault;
    public bool ConditionRandomness { get; set; } = true;
    public bool EnableSynthesisHelper { get; set; } = true;
    public int SynthesisHelperStepCount { get; set; } = 5;

    public Simulator.Simulator CreateSimulator(SimulationState state) =>
        ConditionRandomness ?
            new Simulator.Simulator(state) :
            new SimulatorNoRandom(state);

    public void Save() =>
        Service.PluginInterface.SavePluginConfig(this);
}
