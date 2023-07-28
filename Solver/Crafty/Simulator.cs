using Craftimizer.Simulator;

namespace Craftimizer.Solver.Crafty;

public sealed class Simulator : SimulatorNoRandom
{
    private readonly int maxStepCount;

    public new CompletionState CompletionState => CalculateCompletionState(State, maxStepCount);
    public override bool IsComplete => CompletionState != CompletionState.Incomplete;

    public Simulator(SimulationState state, int maxStepCount) : base(state)
    {
        this.maxStepCount = maxStepCount;
    }

    public static CompletionState CalculateCompletionState(SimulationState state, int maxStepCount) =>
        (state.ActionCount + 1) >= maxStepCount ?
        CompletionState.MaxActionCountReached :
        (CompletionState)CalculateCompletionState(state);
}
