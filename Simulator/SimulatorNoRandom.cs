namespace Craftimizer.Simulator;

public class SimulatorNoRandom : Simulator
{
    public sealed override bool RollSuccessRaw(float successRate) => successRate == 1;
    public sealed override Condition GetNextRandomCondition() => Condition.Normal;
}
