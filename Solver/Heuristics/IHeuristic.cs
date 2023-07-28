using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Heuristics;

public interface IHeuristic
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    abstract static bool ShouldUseAction(Simulator s, ActionType action, BaseAction baseAction);

    [Pure]
    abstract static ActionSet AvailableActions(Simulator s);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static ActionSet AvailableActions<T>(Simulator s, ReadOnlySpan<ActionType> availableActions) where T : IHeuristic
    {
        if (s.IsComplete)
            return new();

        var ret = new ActionSet();
        foreach (var action in availableActions)
        {
            var baseAction = action.Base();
            if (T.ShouldUseAction(s, action, baseAction) && baseAction.CanUse(s))
                ret.AddAction(action);
        }
        return ret;
    }
}
