using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Craftimizer.Plugin.Windows;

public sealed partial class Simulator : Window, IDisposable
{
    private Task? SolverTask { get; set; }
    private CancellationTokenSource? SolverTaskToken { get; set; }
    private ConcurrentQueue<ActionType> SolverActionQueue { get; } = new();
    private int SolverInitialActionCount { get; set; }
    private bool SolverActionsChanged { get; set; } = true;

    private bool CanModifyActions => SolverTask?.IsCompleted ?? true;

    private void OnActionsChanged()
    {
        SolverActionsChanged = true;
    }

    private SimulationState? GenerateSolverState()
    {
        if (Sim is SimulatorNoRandom)
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

    private void StopSolveMacro()
    {
        if (SolverTask == null || SolverTaskToken == null)
            return;

        if (!SolverTask.IsCompleted)
            SolverTaskToken.Cancel();
        else
        {
            SolverTaskToken.Dispose();
            SolverTask.Dispose();

            SolverTask = null;
            SolverTaskToken = null;
        }
    }

    private void SolveMacro(SimulationState solverState)
    {
        StopSolveMacro();

        // Prevents the quality bar from being unfair between solves
        if (Config.ConditionRandomness)
        {
            Config.ConditionRandomness = false;
            Config.Save();

            ResetSimulator();
        }

        SolverActionsChanged = false;

        SolverActionQueue.Clear();

        SolverInitialActionCount = Actions.Count;
        SolverTaskToken = new();
        SolverTask = Task.Run(() => Config.SimulatorSolverConfig.SearchSafely(solverState, SolverActionQueue.Enqueue, SolverTaskToken.Token));
    }

    public void Dispose()
    {
        StopSolveMacro();
        SolverTask?.Wait();
        SolverTask?.Dispose();
        SolverTaskToken?.Dispose();
    }
}
