using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver.Crafty;
using Dalamud.Configuration;
using Dalamud.Logging;
using System;
using System.Collections.Generic;

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

    public Simulator.Simulator CreateSimulator(SimulationState state) =>
        ConditionRandomness ?
            new Simulator.Simulator(state) :
            new SimulatorNoRandom(state);

    public void Save() =>
        Service.PluginInterface.SavePluginConfig(this);
}
