using Craftimizer.Simulator.Actions;
using Dalamud.Interface.Windowing;
using System;

namespace Craftimizer.Plugin.Windows;

public sealed partial class SimulatorWindow : Window, IDisposable
{
    private void AppendAction(ActionType action)
    {
        OnActionsChanged();

        AppendGeneratedAction(action);
    }

    private void AppendGeneratedAction(ActionType action)
    {
        var tooltip = action.Base().GetTooltip(Simulator, false);
        var (response, state) = Simulator.Execute(LatestState, action);
        Actions.Add((action, tooltip, response, state));
    }

    private void RemoveAction(int actionIndex)
    {
        OnActionsChanged();

        // Remove action
        Actions.RemoveAt(actionIndex);

        // Take note of all actions afterwards
        Span<ActionType> succeedingActions = stackalloc ActionType[Actions.Count - actionIndex];
        for (var i = 0; i < succeedingActions.Length; i++)
            succeedingActions[i] = Actions[i + actionIndex].Action;

        // Remove all future actions
        Actions.RemoveRange(actionIndex, succeedingActions.Length);

        // Re-execute all future actions
        foreach (var action in succeedingActions)
            AppendAction(action);
    }

    private void InsertAction(int actionIndex, ActionType action)
    {
        OnActionsChanged();

        // Take note of all actions afterwards
        Span<ActionType> succeedingActions = stackalloc ActionType[Actions.Count - actionIndex];
        for (var i = 0; i < succeedingActions.Length; i++)
            succeedingActions[i] = Actions[i + actionIndex].Action;

        // Remove all future actions
        Actions.RemoveRange(actionIndex, succeedingActions.Length);

        // Execute new action
        AppendAction(action);

        // Re-execute all future actions
        foreach (var succeededAction in succeedingActions)
            AppendAction(succeededAction);
    }

    private void ClearAllActions()
    {
        OnActionsChanged();

        Actions.Clear();
    }

    private void ReexecuteAllActions()
    {
        Span<ActionType> actions = stackalloc ActionType[Actions.Count];
        for (var i = 0; i < actions.Length; i++)
            actions[i] = Actions[i].Action;

        Actions.Clear();
        foreach (var action in actions)
            AppendAction(action);
    }
}
