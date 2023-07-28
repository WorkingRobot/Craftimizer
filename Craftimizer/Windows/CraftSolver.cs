using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Craftimizer.Plugin.Windows;

public sealed unsafe partial class Craft : Window, IDisposable
{
    private SimulationState? SolverState { get; set; }
    private Task? SolverTask { get; set; }
    private CancellationTokenSource? SolverTaskToken { get; set; }
    private ConcurrentQueue<ActionType> SolverActionQueue { get; } = new();

    // State is the state of the simulation *after* its corresponding action is executed.
    private List<(ActionType Action, string Tooltip, SimulationState State)> SolverActions { get; } = new();
    private SimulatorNoRandom SolverSim { get; set; } = null!;
    private SimulationState SolverLatestState => SolverActions.Count == 0 ? SolverState!.Value : SolverActions[^1].State;

    private void StopSolve()
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

    private void QueueSolve(SimulationState state)
    {
        StopSolve();

        SolverActionQueue.Clear();
        SolverActions.Clear();
        SolverState = state;
        SolverSim = new(state);

        SolverTaskToken = new();
        SolverTask = Task.Run(() => Config.SynthHelperSolverConfig.SearchSafely(state, SolverActionQueue.Enqueue, SolverTaskToken.Token));
    }

    private void SolveTick()
    {
        var newState = GetNextState();
        if (SolverState == newState)
            return;

        if (newState == null)
            StopSolve();
        else
            QueueSolve(newState.Value);
    }

    private void DequeueSolver()
    {
        while (SolverActionQueue.TryDequeue(out var poppedAction))
            AppendSolverAction(poppedAction);
    }

    private void AppendSolverAction(ActionType action)
    {
        var actionBase = action.Base();
        if (actionBase is BaseComboAction comboActionBase)
        {
            AppendSolverAction(comboActionBase.ActionTypeA);
            AppendSolverAction(comboActionBase.ActionTypeB);
        }
        else
        {
            if (SolverActions.Count >= Config.SynthHelperStepCount)
            {
                StopSolve();
                return;
            }

            var tooltip = actionBase.GetTooltip(SolverSim, false);
            var (_, state) = SolverSim.Execute(SolverLatestState, action);
            SolverActions.Add((action, tooltip, state));

            if (SolverActions.Count >= Config.SynthHelperStepCount)
                StopSolve();
        }
    }
}
