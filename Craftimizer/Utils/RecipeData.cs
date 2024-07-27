using Craftimizer.Plugin;
using Craftimizer.Simulator;
using ExdSheets;
using Lumina.Excel;
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
    public IReadOnlyList<int?>? CollectableThresholds { get; }
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
            ConditionsFlag = Table.ConditionsFlag,
            MaxDurability = Table.Durability * Recipe.DurabilityFactor / 100,
            MaxQuality = (Recipe.CanHq || Recipe.IsExpert) ? (int)Table.Quality * Recipe.QualityFactor / 100 : 0,
            MaxProgress = Table.Difficulty * Recipe.DifficultyFactor / 100,
            QualityModifier = Table.QualityModifier,
            QualityDivider = Table.QualityDivider,
            ProgressModifier = Table.ProgressModifier,
            ProgressDivider = Table.ProgressDivider,
        };

        int[]? thresholds = null;
        if (Recipe.CollectableMetadata is LazyRow<CollectablesShopRefine> { Value: { } row })
            thresholds = [row.LowCollectability, row.MidCollectability, row.HighCollectability];
        else if (Recipe.CollectableMetadata is LazyRow<HWDCrafterSupply> { Value: { } row2 })
        {
            foreach (var entry in row2.HWDCrafterSupplyParams)
            {
                if (entry.ItemTradeIn.Row == Recipe.ItemResult.Row)
                {
                    thresholds = [entry.BaseCollectableRating, entry.MidCollectableRating, entry.HighCollectableRating];
                    break;
                }
            }
        }
        else if (Recipe.CollectableMetadata is LazyRow<SatisfactionSupply> { } row4)
        {
            var subRowCount = LuminaSheets.SatisfactionSupplySheet.GetSubRowCount(row4.Row);
            if (subRowCount is { } subRowValue)
            {
                for (uint i = 0; i < subRowValue; ++i)
                {
                    var subRow = LuminaSheets.SatisfactionSupplySheet.GetRow(row4.Row, i);
                    if (subRow == null)
                        continue;
                    if (subRow.Item.Row == Recipe.ItemResult.Row)
                    {
                        thresholds = [subRow.CollectabilityLow, subRow.CollectabilityMid, subRow.CollectabilityHigh];
                        break;
                    }
                }
            }
        }
        else if (Recipe.CollectableMetadata is LazyRow<SharlayanCraftWorksSupply> { Value: { } row5 })
        {
            foreach (var item in row5.Item)
            {
                if (item.ItemId.Row == Recipe.ItemResult.Row)
                {
                    thresholds = [0, item.CollectabilityMid, item.CollectabilityHigh];
                    break;
                }
            }
        }
        else if (Recipe.CollectableMetadata is LazyRow<CollectablesRefine> { Value: { } row6 })
        {
            if (row6.CollectabilityHigh != 0)
                thresholds = [row6.CollectabilityLow, row6.CollectabilityMid, row6.CollectabilityHigh];
            else
                thresholds = [0, row6.CollectabilityLow, row6.CollectabilityMid];
        }

        CollectableThresholds = thresholds?.Select<int, int?>(t => t == 0 ? null : t).ToArray();

        Ingredients = Recipe.Ingredient.Zip(Recipe.AmountIngredient)
            .Take(6)
            .Where(i => i.First.Value != null)
            .Select(i => (i.First.Value!, (int)i.Second))
            .ToList();
        MaxStartingQuality = (int)Math.Floor(Recipe.MaterialQualityFactor * RecipeInfo.MaxQuality / 100f);

        TotalHqILvls = (int)Ingredients.Where(i => i.Item.CanBeHq).Sum(i => i.Item.LevelItem.Row * i.Amount);
    }

    public int CalculateItemStartingQuality(int itemIdx, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(itemIdx, Ingredients.Count);

        var ingredient = Ingredients[itemIdx];
        ArgumentOutOfRangeException.ThrowIfGreaterThan(amount, ingredient.Amount);

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
