using Lumina.Excel.GeneratedSheets;
using System;

namespace Craftimizer.Simulator;

public readonly record struct SimulationInput
{
    public CharacterStats Stats { get; init; }
    public Recipe Recipe { get; init; }
    public Random Random { get; init; }

    public RecipeLevelTable RecipeTable => Recipe.RecipeLevelTable.Value!;
    public int RLvl => (int)RecipeTable.RowId;
    public Condition[] AvailableConditions => ConditionUtils.GetPossibleConditions(RecipeTable.ConditionsFlag);

    public int MaxDurability => RecipeTable.Durability * Recipe.DurabilityFactor / 100;
    public int MaxQuality => (int)RecipeTable.Quality * Recipe.QualityFactor / 100;
    public int MaxProgress => RecipeTable.Difficulty * Recipe.DifficultyFactor / 100;
}
