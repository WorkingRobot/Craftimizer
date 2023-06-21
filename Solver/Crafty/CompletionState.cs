using CompState = Craftimizer.Simulator.CompletionState;

namespace Craftimizer.Solver.Crafty;

public enum CompletionState : byte
{
    Incomplete,
    ProgressComplete,
    NoMoreDurability,

    InvalidAction,
    MaxActionCountReached,
    NoMoreActions
}

internal static class CompletionStateUtils
{
    public static CompState IntoBase(this CompletionState me) =>
        (CompState)me >= CompState.Other ? CompState.Other : (CompState)me;
}
