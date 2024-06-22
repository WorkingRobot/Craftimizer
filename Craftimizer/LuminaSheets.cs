using Dalamud;
using ExdSheets;
using Lumina.Excel;
using System.Collections.Concurrent;

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
    public static readonly ExcelSheet<ENpcResident> ENpcResidentSheet = Service.DataManager.GetExcelSheet<ENpcResident>()!;
    public static readonly ExcelSheet<Level> LevelSheet = Service.DataManager.GetExcelSheet<Level>()!;
    public static readonly ExcelSheet<Quest> QuestSheet = Service.DataManager.GetExcelSheet<Quest>()!;
    public static readonly ExcelSheet<Materia> MateriaSheet = Service.DataManager.GetExcelSheet<Materia>()!;
    public static readonly ExcelSheet<BaseParam> BaseParamSheet = Service.DataManager.GetExcelSheet<BaseParam>()!;
    public static readonly ExcelSheet<ItemFood> ItemFoodSheet = Service.DataManager.GetExcelSheet<ItemFood>()!;
    public static readonly ExcelSheet<CollectablesShopRefine> CollectablesShopRefineSheet = Service.DataManager.GetExcelSheet<CollectablesShopRefine>()!;
    public static readonly ExcelSheet<HWDCrafterSupply> HWDCrafterSupplySheet = Service.DataManager.GetExcelSheet<HWDCrafterSupply>()!;
    public static readonly ExcelSheet<SatisfactionSupply> SatisfactionSupplySheet = Service.DataManager.GetExcelSheet<SatisfactionSupply>()!;
    public static readonly ExcelSheet<SharlayanCraftWorksSupply> SharlayanCraftWorksSupplySheet = Service.DataManager.GetExcelSheet<SharlayanCraftWorksSupply>()!;

    private static ConcurrentDictionary<(ExcelSheetImpl, uint), uint> SubRowCountCache { get; } = new();
    public static uint? GetSubRowCount<T>(this ExcelSheet<T> sheet, uint row) where T : ExcelRow
    {
        if (SubRowCountCache.TryGetValue((sheet, row), out var count))
            return count;
        var parser = sheet.GetRowParser(row);
        if (parser == null)
            return null;
        SubRowCountCache.TryAdd((sheet, row), parser.RowCount);
        return parser.RowCount;
    }
}
