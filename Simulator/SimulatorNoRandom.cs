namespace Craftimizer.Simulator;

public class SimulatorNoRandom : Simulator
{
    public sealed override bool RollSuccessRaw(int successRate) => successRate == 100;
    public sealed override Condition GetNextRandomCondition() => Condition.Normal;
}
