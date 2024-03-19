using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Lumina.Excel.GeneratedSheets2;
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

        CollectableThresholds = null;
        switch (Recipe.Unknown1)
        {
            case 1:
                var data1 = LuminaSheets.CollectablesShopRefineSheet.GetRow(Recipe.Unknown0);
                if (data1 == null)
                    break;
                CollectableThresholds = new int?[] { data1.LowCollectability, data1.MidCollectability, data1.HighCollectability };
                break;
            case 2:
                var data2 = LuminaSheets.HWDCrafterSupplySheet.GetRow(Recipe.Unknown0);
                if (data2 == null)
                    break;
                foreach (var entry in data2.HWDCrafterSupplyParams)
                {
                    if (entry.ItemTradeIn.Row == Recipe.ItemResult.Row)
                    {
                        CollectableThresholds = new int?[] { entry.BaseCollectableRating, entry.MidCollectableRating, entry.HighCollectableRating };
                        break;
                    }
                }
                break;
            case 3:
                var subRowCount = LuminaSheets.SatisfactionSupplySheet.GetSubRowCount(Recipe.Unknown0);
                if (subRowCount is not { } subRowValue)
                    break;
                for (uint i = 0; i < subRowValue; ++i)
                {
                    var data3 = LuminaSheets.SatisfactionSupplySheet.GetRow(Recipe.Unknown0, i);
                    if (data3 == null)
                        continue;
                    if (data3.Item.Row == Recipe.ItemResult.Row)
                    {
                        CollectableThresholds = new int?[] { data3.CollectabilityLow, data3.CollectabilityMid, data3.CollectabilityHigh };
                        break;
                    }
                }
                break;
            case 4:
                var data4 = LuminaSheets.SharlayanCraftWorksSupplySheet.GetRow(Recipe.Unknown0);
                if (data4 == null)
                    break;
                foreach (var item in data4.Items)
                {
                    if (item.Item.Row == Recipe.ItemResult.Row)
                    {
                        CollectableThresholds = new int?[] { null, item.CollectabilityMid, item.CollectabilityHigh };
                        break;
                    }
                }
                break;
            default:
                break;
        }

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
