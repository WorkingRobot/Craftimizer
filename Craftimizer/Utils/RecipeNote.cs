using Craftimizer.Plugin;
using Craftimizer.Simulator;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using System;
using ClassJob = Craftimizer.Simulator.ClassJob;
using CSRecipeNote = FFXIVClientStructs.FFXIV.Client.Game.UI.RecipeNote;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Craftimizer.Utils;

public unsafe class RecipeNote
{
    public AddonRecipeNote* AddonRecipe { get; private set; }
    public AddonSynthesis* AddonSynthesis { get; private set; }
    public CSRecipeNote* State { get; private set; }
    public ushort RecipeId { get; private set; }
    public Recipe Recipe { get; private set; } = null!;

    public RecipeLevelTable Table { get; private set; } = null!;
    public RecipeInfo Info { get; private set; } = null!;
    public ClassJob ClassJob { get; private set; }
    public short CharacterLevel { get; private set; }
    public bool CanUseManipulation { get; private set; }
    public int HQIngredientCount { get; private set; }
    public int MaxStartingQuality { get; private set; }

    public RecipeNote()
    {
        
    }

    public bool Update(out bool isNewRecipe)
    {
        isNewRecipe = false;

        if (Service.ClientState.LocalPlayer == null)
            return false;

        AddonRecipe = (AddonRecipeNote*)Service.GameGui.GetAddonByName("RecipeNote");
        AddonSynthesis = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis");

        State = CSRecipeNote.Instance();

        var list = State->RecipeList;

        if (list == null)
            return false;

        var recipeEntry = list->SelectedRecipe;

        if (recipeEntry == null)
            return false;

        isNewRecipe = RecipeId != recipeEntry->RecipeId;

        RecipeId = recipeEntry->RecipeId;

        var recipe = LuminaSheets.RecipeSheet.GetRow(RecipeId);

        if (recipe == null)
            return false;

        Recipe = recipe;

        if (isNewRecipe)
            CalculateStats();

        return true;
    }

    private void CalculateStats()
    {
        Table = Recipe.RecipeLevelTable.Value!;
        Info = CreateInfo();
        ClassJob = (ClassJob)Recipe.CraftType.Row;
        CharacterLevel = PlayerState.Instance()->ClassJobLevelArray[ClassJob.GetClassJobIndex()];
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
}
