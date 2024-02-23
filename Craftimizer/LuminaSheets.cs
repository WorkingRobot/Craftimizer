using Dalamud;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Concurrent;
using Action = Lumina.Excel.GeneratedSheets.Action;

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

#nullable disable

[Sheet("SharlayanCraftWorksSupply", columnHash: 0x903b128e)]
public class SharlayanCraftWorksSupply : ExcelRow
{
    public class ItemData
    {
        public byte Level { get; set; }
        public LazyRow<Item> Item { get; set; }
        public ushort CollectabilityMid { get; set; }
        public ushort CollectabilityHigh { get; set; }
        public uint XPReward { get; set; }
        public byte HighXPMultiplier { get; set; }
        public ushort GilReward { get; set; }
        public byte HighGilMultiplier { get; set; }
        public byte Unknown8 { get; set; }
        public byte ScripReward { get; set; }
        public byte HighScripMultiplier { get; set; }
    }

    public ItemData[] Items { get; set; }

    public override void PopulateData(RowParser parser, GameData gameData, Language language)
    {
        base.PopulateData(parser, gameData, language);

        Items = new ItemData[4];
        for (var i = 0; i < 4; i++)
        {
            Items[i] = new ItemData();
            Items[i].Level = parser.ReadColumn<byte>(0 + (i * 11 + 0));
            Items[i].Item = new LazyRow<Item>(gameData, parser.ReadColumn<uint>(0 + (i * 11 + 1)), language);
            Items[i].CollectabilityMid = parser.ReadColumn<ushort>(0 + (i * 11 + 2));
            Items[i].CollectabilityHigh = parser.ReadColumn<ushort>(0 + (i * 11 + 3));
            Items[i].XPReward = parser.ReadColumn<uint>(0 + (i * 11 + 4));
            Items[i].HighXPMultiplier = parser.ReadColumn<byte>(0 + (i * 11 + 5));
            Items[i].GilReward = parser.ReadColumn<ushort>(0 + (i * 11 + 6));
            Items[i].HighGilMultiplier = parser.ReadColumn<byte>(0 + (i * 11 + 7));
            Items[i].Unknown8 = parser.ReadColumn<byte>(0 + (i * 11 + 8));
            Items[i].ScripReward = parser.ReadColumn<byte>(0 + (i * 11 + 9));
            Items[i].HighScripMultiplier = parser.ReadColumn<byte>(0 + (i * 11 + 10));
        }
    }
}

#nullable restore
