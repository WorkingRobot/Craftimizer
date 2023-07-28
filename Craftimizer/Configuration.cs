using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;
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
            Solver.Solver.Search(me, state, actionCallback, token);
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
    public bool ConditionRandomness { get; set; } = true;
    public SolverConfig SimulatorSolverConfig { get; set; } = SolverConfig.SimulatorDefault;
    public SolverConfig SynthHelperSolverConfig { get; set; } = SolverConfig.SynthHelperDefault;
    public bool EnableSynthHelper { get; set; } = true;
    public int SynthHelperStepCount { get; set; } = 5;

    public Simulator.Simulator CreateSimulator(SimulationState state) =>
        ConditionRandomness ?
            new Simulator.Simulator(state) :
            new SimulatorNoRandom(state);

    public void Save() =>
        Service.PluginInterface.SavePluginConfig(this);
}
