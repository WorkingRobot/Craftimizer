namespace Craftimizer.Simulator;

public sealed class SimulatorNoRandom : ISimulator
{
    private SimulatorNoRandom() { }

    public static Condition GetNextRandomCondition<S>(Simulator<S> s) where S : ISimulator =>
       Condition.Normal;

    public static bool RollSuccessRaw<S>(Simulator<S> s, float successRate) where S : ISimulator =>
       successRate == 1;
}
