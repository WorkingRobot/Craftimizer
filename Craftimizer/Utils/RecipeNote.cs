using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Dalamud.Game;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using ClassJob = Craftimizer.Simulator.ClassJob;
using CSRecipeNote = FFXIVClientStructs.FFXIV.Client.Game.UI.RecipeNote;

namespace Craftimizer.Utils;

public record RecipeData
{
    public ushort RecipeId { get; }

    public Recipe Recipe { get; }
    public RecipeLevelTable Table { get; }

    public ClassJob ClassJob { get; }
    public RecipeInfo RecipeInfo { get; }
    public int HQIngredientCount { get; }
    public int MaxStartingQuality { get; }

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
            MaxQuality = (int)Table.Quality * Recipe.QualityFactor / 100,
            MaxProgress = Table.Difficulty * Recipe.DifficultyFactor / 100,
            QualityModifier = Table.QualityModifier,
            QualityDivider = Table.QualityDivider,
            ProgressModifier = Table.ProgressModifier,
            ProgressDivider = Table.ProgressDivider,
        };

        HQIngredientCount = Recipe.UnkData5
            .Where(i =>
                i != null &&
                i.ItemIngredient != 0 &&
                (LuminaSheets.ItemSheet.GetRow((uint)i.ItemIngredient)?.CanBeHq ?? false)
            ).Sum(i => i.AmountIngredient);
        MaxStartingQuality = (int)Math.Floor(Recipe.MaterialQualityFactor * RecipeInfo.MaxQuality / 100f);
    }
}

public sealed unsafe class RecipeNote : IDisposable
{
    public AddonRecipeNote* AddonRecipe { get; private set; }
    public AddonSynthesis* AddonSynthesis { get; private set; }
    public bool IsCrafting { get; private set; }
    public ushort RecipeId { get; private set; }
    public Recipe Recipe { get; private set; } = null!;
    public bool HasValidRecipe { get; private set; }

    public RecipeLevelTable Table { get; private set; } = null!;
    public RecipeInfo Info { get; private set; } = null!;
    public ClassJob ClassJob { get; private set; }
    public short CharacterLevel { get; private set; }
    public bool CanUseManipulation { get; private set; }
    public int HQIngredientCount { get; private set; }
    public int MaxStartingQuality { get; private set; }

    public RecipeNote()
    {
        Service.Framework.Update += FrameworkUpdate;
    }

    private void FrameworkUpdate(Framework f)
    {
        HasValidRecipe = false;
        try
        {
            HasValidRecipe = Update();
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "RecipeNote framework update failed");
        }
    }

    public bool Update()
    {
        if (Service.ClientState.LocalPlayer == null)
            return false;

        AddonRecipe = (AddonRecipeNote*)Service.GameGui.GetAddonByName("RecipeNote");
        AddonSynthesis = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis");

        var recipeId = GetRecipeIdFromList();
        if (recipeId == null)
        {
            recipeId = GetRecipeIdFromAgent();
            if (recipeId == null)
                return false;
            else
                IsCrafting = true;
        }
        else
            IsCrafting = false;

        var isNewRecipe = RecipeId != recipeId.Value;

        RecipeId = recipeId.Value;

        var recipe = LuminaSheets.RecipeSheet.GetRow(RecipeId);

        if (recipe == null)
            return false;

        Recipe = recipe;

        if (isNewRecipe)
            CalculateStats();

        return true;
    }

    private static ushort? GetRecipeIdFromList()
    {
        var instance = CSRecipeNote.Instance();

        var list = instance->RecipeList;

        if (list == null)
            return null;

        var recipeEntry = list->SelectedRecipe;

        if (recipeEntry == null)
            return null;

        return recipeEntry->RecipeId;
    }

    private static ushort? GetRecipeIdFromAgent()
    {
        var instance = AgentRecipeNote.Instance();

        var recipeId = instance->ActiveCraftRecipeId;

        if (recipeId == 0)
            return null;

        return (ushort)recipeId;
    }

    private void CalculateStats()
    {
        Table = Recipe.RecipeLevelTable.Value!;
        Info = CreateInfo();
        ClassJob = (ClassJob)Recipe.CraftType.Row;
        CharacterLevel = PlayerState.Instance()->ClassJobLevelArray[ClassJob.GetExpArrayIdx()];
        CanUseManipulation = ActionManager.CanUseActionOnTarget(ActionType.Manipulation.GetId(ClassJob), (GameObject*)Service.ClientState.LocalPlayer!.Address);
        HQIngredientCount = Recipe.UnkData5
            .Where(i =>
                i != null &&
                i.ItemIngredient != 0 &&
                (LuminaSheets.ItemSheet.GetRow((uint)i.ItemIngredient)?.CanBeHq ?? false)
            ).Sum(i => i.AmountIngredient);
        MaxStartingQuality = (int)Math.Floor(Recipe.MaterialQualityFactor * Info.MaxQuality / 100f);
    }

    private RecipeInfo CreateInfo() =>
        new()
        {
            IsExpert = Recipe.IsExpert,
            ClassJobLevel = Table.ClassJobLevel,
            RLvl = (int)Table.RowId,
            ConditionsFlag = Table.ConditionsFlag,
            MaxDurability = Table.Durability * Recipe.DurabilityFactor / 100,
            MaxQuality = (int)Table.Quality * Recipe.QualityFactor / 100,
            MaxProgress = Table.Difficulty * Recipe.DifficultyFactor / 100,
            QualityModifier = Table.QualityModifier,
            QualityDivider = Table.QualityDivider,
            ProgressModifier = Table.ProgressModifier,
            ProgressDivider = Table.ProgressDivider,
        };

    public void Dispose()
    {
        Service.Framework.Update -= FrameworkUpdate;
    }
}
