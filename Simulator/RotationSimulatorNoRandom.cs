namespace Craftimizer.Simulator;

public class RotationSimulatorNoRandom : RotationSimulator
{
    public sealed override bool RollSuccessRaw(int successRate) => successRate == 100;
    public sealed override Condition GetNextRandomCondition() => Condition.Normal;
}
