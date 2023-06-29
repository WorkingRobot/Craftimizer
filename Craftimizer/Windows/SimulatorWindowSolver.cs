using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Craftimizer.Plugin.Windows;

public sealed partial class SimulatorWindow : Window, IDisposable
{
    private Task SolverTask { get; set; } = Task.CompletedTask;
    private CancellationTokenSource SolverTaskToken { get; set; } = new();
    private ConcurrentQueue<ActionType> SolverActionQueue { get; } = new();
    private int SolverInitialActionCount { get; set; }
    private bool SolverActionsChanged { get; set; } = true;

    private void OnActionsChanged()
    {
        SolverActionsChanged = true;
    }

    private SimulationState? GenerateSolverState()
    {
        if (Simulator is SimulatorNoRandom)
        {
            if (!Actions.Exists(a => a.Response != ActionResponse.UsedAction))
                return LatestState;
            else
                return null;
        }

        var ret = new SimulationState(Input);
        if (Actions.Count != 0)
        {
            var tmpSim = new SimulatorNoRandom(ret);
            foreach (var action in Actions)
            {
                (var resp, ret) = tmpSim.Execute(ret, action.Action);
                if (resp != ActionResponse.UsedAction)
                    return null;
            }
        }
        return ret;
    }

    private void SolveMacro(SimulationState solverState)
    {
        if (!SolverTask.IsCompleted)
        {
            SolverTaskToken.Cancel();
        }

        // Prevents the quality bar from being unfair between solves
        if (Configuration.ConditionRandomness)
        {
            Configuration.ConditionRandomness = false;
            Configuration.Save();

            ResetSimulator();
        }

        SolverActionsChanged = false;

        SolverTaskToken.Dispose();
        SolverTask.Dispose();
        SolverActionQueue.Clear();

        SolverInitialActionCount = Actions.Count;
        SolverTaskToken = new();
        SolverTask = Task.Run(() => Solver.Crafty.Solver.SearchStepwise(Service.Configuration.SolverConfig, solverState, SolverActionQueue.Enqueue, SolverTaskToken.Token));
    }

    public void Dispose()
    {
        SolverTask.Dispose();
        SolverTaskToken.Dispose();
    }
}
