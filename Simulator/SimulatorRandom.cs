namespace Craftimizer.Simulator;

public class SimulatorRandom : ISimulator
{
    private SimulatorRandom() { }

    public static Condition GetNextRandomCondition<S>(Simulator<S> s) where S : ISimulator
    {
        static float GetConditionChance(SimulationInput input, Condition condition) =>
            condition switch
            {
                Condition.Good => input.Recipe.IsExpert ? 0.12f : (input.Stats.Level >= 63 ? 0.15f : 0.18f),
                Condition.Excellent => 0.04f,
                Condition.Centered => 0.15f,
                Condition.Sturdy => 0.15f,
                Condition.Pliant => 0.10f,
                Condition.Malleable => 0.13f,
                Condition.Primed => 0.15f,
                Condition.GoodOmen => 0.12f, // https://github.com/ffxiv-teamcraft/simulator/issues/77
                _ => 0.00f
            };

        var conditionChance = s.Input.Random.NextSingle();

        foreach (var condition in s.Input.AvailableConditions)
            if ((conditionChance -= GetConditionChance(s.Input, condition)) < 0)
                return condition;

        return Condition.Normal;
    }

    public static bool RollSuccessRaw<S>(Simulator<S> s, float successRate) where S : ISimulator =>
        successRate >= s.Input.Random.NextSingle();
}
