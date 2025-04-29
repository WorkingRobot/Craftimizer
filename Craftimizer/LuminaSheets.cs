using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Craftimizer.Plugin;

public static class LuminaSheets
{
    private static readonly ExcelModule Module = Service.DataManager.GameData.Excel;

    public static readonly ExcelSheet<Recipe> RecipeSheet = Module.GetSheet<Recipe>();
    public static readonly ExcelSheet<Action> ActionSheet = Module.GetSheet<Action>();
    public static readonly ExcelSheet<CraftAction> CraftActionSheet = Module.GetSheet<CraftAction>();
    public static readonly ExcelSheet<Status> StatusSheet = Module.GetSheet<Status>();
    public static readonly ExcelSheet<Addon> AddonSheet = Module.GetSheet<Addon>();
    public static readonly ExcelSheet<ClassJob> ClassJobSheet = Module.GetSheet<ClassJob>();
    public static readonly ExcelSheet<Item> ItemSheet = Module.GetSheet<Item>();
    public static readonly ExcelSheet<Item> ItemSheetEnglish = Module.GetSheet<Item>(Language.English)!;
    public static readonly ExcelSheet<Level> LevelSheet = Module.GetSheet<Level>();
    public static readonly ExcelSheet<Quest> QuestSheet = Module.GetSheet<Quest>();
    public static readonly ExcelSheet<Materia> MateriaSheet = Module.GetSheet<Materia>();
    public static readonly ExcelSheet<BaseParam> BaseParamSheet = Module.GetSheet<BaseParam>();
    public static readonly ExcelSheet<ItemFood> ItemFoodSheet = Module.GetSheet<ItemFood>();
    public static readonly ExcelSheet<WKSMissionToDoEvalutionRefin> WKSMissionToDoEvalutionRefinSheet = Module.GetSheet<WKSMissionToDoEvalutionRefin>();
    public static readonly ExcelSheet<RecipeLevelTable> RecipeLevelTableSheet = Module.GetSheet<RecipeLevelTable>();
    public static readonly ExcelSheet<GathererCrafterLvAdjustTable> GathererCrafterLvAdjustTableSheet = Module.GetSheet<GathererCrafterLvAdjustTable>();
}
