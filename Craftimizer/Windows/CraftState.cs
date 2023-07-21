using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Craftimizer.Plugin.Windows;

public sealed unsafe partial class Craft : Window, IDisposable
{
    private ConcurrentQueue<ActionType> UsedActionQueue { get; set; } = new();
    private IEnumerator<SimulationState>? StateTicker { get; set; }

    private SimulationState? GetNextState()
    {
        if (RecipeUtils.IsCrafting && StateTicker == null)
            StateTicker = TickState();
        if (!RecipeUtils.IsCrafting && StateTicker != null)
            StateTicker = null;

        if (StateTicker == null)
            return null;
        StateTicker.MoveNext();
        return StateTicker.Current;
    }

    private IEnumerator<SimulationState> TickState()
    {
        while (true)
        {
            SimulationState state;

            // Dequeue used actions
            var sim = new SimulatorNoRandom(new());
            while (true)
            {
                state = GetAddonSimulationState();

                var dequeued = false;
                while (UsedActionQueue.TryDequeue(out var action))
                {
                    dequeued = true;
                    (_, state) = sim.Execute(state, action);
                    ActionCount++;
                    ActionStates.MutateState(action.Base());
                }
                if (dequeued)
                    break;

                // If nothing is dequeued and executed, just return the addon state
                yield return state;
            }

            // Intermediate state, wait for addon change
            var intermediateState = GetAddonSimulationState();
            while (true)
            {
                yield return state;
                var newState = GetAddonSimulationState();
                if (!IsStateInIntermediate(newState, intermediateState))
                    break;
            }
        }
    }

    private static bool IsStateInIntermediate(SimulationState a, SimulationState b)
    {
        b.CP = a.CP;
        b.ActiveEffects = a.ActiveEffects;
        return a == b;
    }

    private void OnActionUsed(ActionType action)
    {
        if (!RecipeUtils.IsCrafting || RecipeUtils.AddonSynthesis == null)
            return;

        UsedActionQueue.Enqueue(action);
    }
}
