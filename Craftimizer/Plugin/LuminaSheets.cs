using Lumina.Excel.GeneratedSheets;
using Lumina.Excel;

namespace Craftimizer.Plugin;

public static class LuminaSheets
{
    public static readonly ExcelSheet<Recipe> RecipeSheet = Service.DataManager.GetExcelSheet<Recipe>()!;
    public static readonly ExcelSheet<RecipeLevelTable> RecipeLevelTableSheet = Service.DataManager.GetExcelSheet<RecipeLevelTable>()!;
    public static readonly ExcelSheet<ParamGrow> ParamGrowSheet = Service.DataManager.GetExcelSheet<ParamGrow>()!;
    public static readonly ExcelSheet<Action> ActionSheet = Service.DataManager.GetExcelSheet<Action>()!;
    public static readonly ExcelSheet<CraftAction> CraftActionSheet = Service.DataManager.GetExcelSheet<CraftAction>()!;
    public static readonly ExcelSheet<Status> StatusSheet = Service.DataManager.GetExcelSheet<Status>()!;
}
