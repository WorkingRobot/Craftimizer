using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.Excel;
using Lumina.Excel.Sheets;
using Lumina.Data;
using Lumina.Excel;

namespace Craftimizer.Plugin;

public static class LuminaSheets
{
    private static readonly ExcelModule ExcelModule = new(Service.DataManager.GameData);

    public static readonly ExcelSheet<Recipe> RecipeSheet = ExcelModule.GetSheet<Recipe>();
    public static readonly ExcelSheet<Action> ActionSheet = ExcelModule.GetSheet<Action>();
    public static readonly ExcelSheet<CraftAction> CraftActionSheet = ExcelModule.GetSheet<CraftAction>();
    public static readonly ExcelSheet<Status> StatusSheet = ExcelModule.GetSheet<Status>();
    public static readonly ExcelSheet<Addon> AddonSheet = ExcelModule.GetSheet<Addon>();
    public static readonly ExcelSheet<ClassJob> ClassJobSheet = ExcelModule.GetSheet<ClassJob>();
    public static readonly ExcelSheet<Item> ItemSheet = ExcelModule.GetSheet<Item>();
    public static readonly ExcelSheet<Item> ItemSheetEnglish = ExcelModule.GetSheet<Item>(Language.English)!;
    public static readonly ExcelSheet<ENpcResident> ENpcResidentSheet = ExcelModule.GetSheet<ENpcResident>();
    public static readonly ExcelSheet<Level> LevelSheet = ExcelModule.GetSheet<Level>();
    public static readonly ExcelSheet<Quest> QuestSheet = ExcelModule.GetSheet<Quest>();
    public static readonly ExcelSheet<Materia> MateriaSheet = ExcelModule.GetSheet<Materia>();
    public static readonly ExcelSheet<BaseParam> BaseParamSheet = ExcelModule.GetSheet<BaseParam>();
    public static readonly ExcelSheet<ItemFood> ItemFoodSheet = ExcelModule.GetSheet<ItemFood>();
    public static readonly SubrowExcelSheet<SatisfactionSupply> SatisfactionSupplySheet = ExcelModule.GetSubrowSheet<SatisfactionSupply>();
}
