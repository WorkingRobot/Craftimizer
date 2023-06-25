using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
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

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool OverrideUncraftability { get; set; } = true;
    public List<Macro> Macros { get; set; } = new();
    public string SimulatorType { get; set; } = typeof(Simulator.Simulator).AssemblyQualifiedName!;

    public Simulator.Simulator CreateSimulator(SimulationState state)
    {
        var type = Type.GetType(SimulatorType);
        if (type == null)
            PluginLog.LogError($"Failed to resolve simulator type ({SimulatorType})");
        else
        {
            if (Activator.CreateInstance(type, state) is Simulator.Simulator sim)
                return sim;

            PluginLog.LogError($"Failed to create simulator ({SimulatorType})");
        }
        return new Simulator.Simulator(state);
    }

    public void Save() =>
        Service.PluginInterface.SavePluginConfig(this);
}
