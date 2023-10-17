using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Utils;

public sealed record RecipeData
{
    public ushort RecipeId { get; }

    public Recipe Recipe { get; }
    public RecipeLevelTable Table { get; }

    public ClassJob ClassJob { get; }
    public RecipeInfo RecipeInfo { get; }
    public IReadOnlyList<(Item Item, int Amount)> Ingredients { get; }
    public int MaxStartingQuality { get; }
    private int TotalHqILvls { get; }

    public RecipeData(ushort recipeId)
    {
        RecipeId = recipeId;

        Recipe = LuminaSheets.RecipeSheet.GetRow(recipeId) ??
            throw new ArgumentException($"Invalid recipe id {recipeId}", nameof(recipeId));

        Table = Recipe.RecipeLevelTable.Value!;
        ClassJob = (ClassJob)Recipe.CraftType.Row;
        RecipeInfo = new()
        {
            IsExpert = Recipe.IsExpert,
            ClassJobLevel = Table.ClassJobLevel,
            RLvl = (int)Table.RowId,
            ConditionsFlag = Table.ConditionsFlag,
            MaxDurability = Table.Durability * Recipe.DurabilityFactor / 100,
            MaxQuality = (Recipe.CanHq || Recipe.IsExpert) ? (int)Table.Quality * Recipe.QualityFactor / 100 : 0,
            MaxProgress = Table.Difficulty * Recipe.DifficultyFactor / 100,
            QualityModifier = Table.QualityModifier,
            QualityDivider = Table.QualityDivider,
            ProgressModifier = Table.ProgressModifier,
            ProgressDivider = Table.ProgressDivider,
        };

        Ingredients = Recipe.UnkData5.Take(6)
            .Where(i => i != null && i.ItemIngredient != 0)
            .Select(i => (LuminaSheets.ItemSheet.GetRow((uint)i.ItemIngredient)!, (int)i.AmountIngredient))
            .Where(i => i.Item1 != null).ToList();
        MaxStartingQuality = (int)Math.Floor(Recipe.MaterialQualityFactor * RecipeInfo.MaxQuality / 100f);

        TotalHqILvls = (int)Ingredients.Where(i => i.Item.CanBeHq).Sum(i => i.Item.LevelItem.Row * i.Amount);
    }

    public int CalculateItemStartingQuality(int itemIdx, int amount)
    {
        if (itemIdx >= Ingredients.Count)
            throw new ArgumentOutOfRangeException(nameof(itemIdx));

        var ingredient = Ingredients[itemIdx];
        if (amount > ingredient.Amount)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (!ingredient.Item.CanBeHq)
            return 0;

        var iLvls = ingredient.Item.LevelItem.Row * amount;
        return (int)Math.Floor((float)iLvls / TotalHqILvls * MaxStartingQuality);
    }

    public int CalculateStartingQuality(IEnumerable<int> hqQuantities)
    {
        if (TotalHqILvls == 0)
            return 0;

        var iLvls = Ingredients.Zip(hqQuantities).Sum(i => i.First.Item.LevelItem.Row * i.Second);

        return (int)Math.Floor((float)iLvls / TotalHqILvls * MaxStartingQuality);
    }
}
