namespace Craftimizer.Simulator;

public interface ISimulator
{
    static CompletionState GetCompletionStateBase<S>(Simulator<S> s) where S : ISimulator
    {
        if (s.Progress >= s.Input.Recipe.MaxProgress)
            return CompletionState.ProgressComplete;
        if (s.Durability <= 0)
            return CompletionState.NoMoreDurability;
        return CompletionState.Incomplete;
    }

    virtual static CompletionState GetCompletionState<S>(Simulator<S> s) where S : ISimulator =>
        GetCompletionStateBase(s);

    abstract static Condition GetNextRandomCondition<S>(Simulator<S> s) where S : ISimulator;

    abstract static bool RollSuccessRaw<S>(Simulator<S> s, float successRate) where S : ISimulator;
}
