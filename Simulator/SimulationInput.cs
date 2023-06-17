using System;

namespace Craftimizer.Simulator;

public readonly record struct SimulationInput
{
    public CharacterStats Stats { get; init; }
    public RecipeInfo Recipe { get; init; }
    public Random Random { get; init; }

    public Condition[] AvailableConditions => ConditionUtils.GetPossibleConditions(Recipe.ConditionsFlag);
}
