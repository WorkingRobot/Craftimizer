using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver.Crafty;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    private bool CanModifyActions => SolverTask.IsCompleted;

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
        Func<SolverConfig, SimulationState, Action<ActionType>?, CancellationToken, SolverSolution> solverMethod = Configuration.SolverAlgorithm switch
        {
            SolverAlgorithm.Oneshot => Solver.Crafty.Solver.SearchOneshot,
            SolverAlgorithm.OneshotForked => Solver.Crafty.Solver.SearchOneshotForked,
            SolverAlgorithm.Stepwise => Solver.Crafty.Solver.SearchStepwise,
            SolverAlgorithm.StepwiseForked => Solver.Crafty.Solver.SearchStepwiseForked,
            SolverAlgorithm.StepwiseFurcated or _ => Solver.Crafty.Solver.SearchStepwiseFurcated,
        };
        SolverTask = Task.Run(() => solverMethod(Configuration.SolverConfig, solverState, SolverActionQueue.Enqueue, SolverTaskToken.Token));
    }

    public void Dispose()
    {
        SolverTask.Dispose();
        SolverTaskToken.Dispose();
    }
}
