using Dalamud;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace Craftimizer.Plugin;

public static class LuminaSheets
{
    public static readonly ExcelSheet<Recipe> RecipeSheet = Service.DataManager.GetExcelSheet<Recipe>()!;
    public static readonly ExcelSheet<RecipeLevelTable> RecipeLevelTableSheet = Service.DataManager.GetExcelSheet<RecipeLevelTable>()!;
    public static readonly ExcelSheet<ParamGrow> ParamGrowSheet = Service.DataManager.GetExcelSheet<ParamGrow>()!;
    public static readonly ExcelSheet<Action> ActionSheet = Service.DataManager.GetExcelSheet<Action>()!;
    public static readonly ExcelSheet<CraftAction> CraftActionSheet = Service.DataManager.GetExcelSheet<CraftAction>()!;
    public static readonly ExcelSheet<Status> StatusSheet = Service.DataManager.GetExcelSheet<Status>()!;
    public static readonly ExcelSheet<Addon> AddonSheet = Service.DataManager.GetExcelSheet<Addon>()!;
    public static readonly ExcelSheet<ClassJob> ClassJobSheet = Service.DataManager.GetExcelSheet<ClassJob>()!;
    public static readonly ExcelSheet<Item> ItemSheet = Service.DataManager.GetExcelSheet<Item>()!;
    public static readonly ExcelSheet<Item> ItemSheetEnglish = Service.DataManager.GetExcelSheet<Item>(ClientLanguage.English)!;
    public static readonly ExcelSheet<Materia> MateriaSheet = Service.DataManager.GetExcelSheet<Materia>()!;
    public static readonly ExcelSheet<BaseParam> BaseParamSheet = Service.DataManager.GetExcelSheet<BaseParam>()!;
    public static readonly ExcelSheet<ItemFood> ItemFoodSheet = Service.DataManager.GetExcelSheet<ItemFood>()!;
}
