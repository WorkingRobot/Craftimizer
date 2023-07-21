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

public enum SolverAlgorithm
{
    Oneshot,
    OneshotForked,
    Stepwise,
    StepwiseForked,
    StepwiseFurcated,
}

public static class AlgorithmUtils
{
    public static void Invoke(this SolverAlgorithm me, SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
    {
        Func<SolverConfig, SimulationState, Action<ActionType>?, CancellationToken, SolverSolution> func = me switch
        {
            SolverAlgorithm.Oneshot => Solver.Crafty.Solver.SearchOneshot,
            SolverAlgorithm.OneshotForked => Solver.Crafty.Solver.SearchOneshotForked,
            SolverAlgorithm.Stepwise => Solver.Crafty.Solver.SearchStepwise,
            SolverAlgorithm.StepwiseForked => Solver.Crafty.Solver.SearchStepwiseForked,
            SolverAlgorithm.StepwiseFurcated or _ => Solver.Crafty.Solver.SearchStepwiseFurcated,
        };
        try
        {
            func(config, state, actionCallback, token);
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
    public SolverConfig SolverConfig { get; set; } = new();
    public SolverAlgorithm SolverAlgorithm { get; set; } = SolverAlgorithm.StepwiseFurcated;
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
