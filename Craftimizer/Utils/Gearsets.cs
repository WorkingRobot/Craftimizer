using Craftimizer.Simulator;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ExdSheets;
using System;
using System.Linq;

namespace Craftimizer.Plugin.Utils;
public static unsafe class Gearsets
{
    public record struct GearsetStats(int CP, int Craftsmanship, int Control);
    public record struct GearsetMateria(ushort Type, ushort Grade);
    public record struct GearsetItem(uint itemId, bool isHq, GearsetMateria[] materia);

    private static readonly GearsetStats BaseStats = new(180, 0, 0);

    public const uint ParamCP = 11;
    public const uint ParamCraftsmanship = 70;
    public const uint ParamControl = 71;

    private static readonly int[] LevelToCLvlLUT;

    static Gearsets()
    {
        LevelToCLvlLUT = new int[100];
        for (uint i = 0; i < 80; ++i) {
            var level = i + 1;
            LevelToCLvlLUT[i] = LuminaSheets.ParamGrowSheet.GetRow(level)!.CraftingLevel;
        }
        for (var i = 80; i < 100; ++i)
        {
            var level = i + 1;
            LevelToCLvlLUT[i] = (int)LuminaSheets.RecipeLevelTableSheet.First(r => r.ClassJobLevel == level).RowId;
        }
    }

    public static void Initialize() { }

    public static GearsetItem[] GetGearsetItems(InventoryContainer* container)
    {
        var items = new GearsetItem[(int)container->Size];
        for (var i = 0; i < container->Size; ++i)
        {
            var item = container->Items[i];
            items[i] = new(item.ItemID, item.Flags.HasFlag(InventoryItem.ItemFlags.HQ), GetMaterias(item.Materia, item.MateriaGrade));
        }
        return items;
    }

    public static GearsetItem[] GetGearsetItems(RaptureGearsetModule.GearsetEntry* entry)
    {
        var gearsetItems = entry->ItemsSpan;
        var items = new GearsetItem[14];
        for (var i = 0; i < 14; ++i)
        {
            var item = gearsetItems[i];
            items[i] = new(item.ItemID % 1000000, item.ItemID > 1000000, GetMaterias(item.Materia, item.MateriaGrade));
        }
        return items;
    }

    public static GearsetStats CalculateGearsetItemStats(GearsetItem gearsetItem)
    {
        var item = LuminaSheets.ItemSheet.GetRow(gearsetItem.itemId)!;

        int cp = 0, craftsmanship = 0, control = 0;

        void IncreaseStat(uint baseParam, int amount)
        {
            if (baseParam == ParamCP)
                cp += amount;
            else if (baseParam == ParamCraftsmanship)
                craftsmanship += amount;
            else if (baseParam == ParamControl)
                control += amount;
        }

        foreach (var statIncrease in item.BaseParam.Zip(item.BaseParamValue))
            IncreaseStat(statIncrease.First.Row, statIncrease.Second);
        if (gearsetItem.isHq)
            foreach (var statIncrease in item.BaseParamSpecial.Zip(item.BaseParamValueSpecial))
                IncreaseStat(statIncrease.First.Row, statIncrease.Second);

        foreach (var gearsetMateria in gearsetItem.materia)
        {
            if (gearsetMateria.Type == 0)
                continue;

            var materia = LuminaSheets.MateriaSheet.GetRow(gearsetMateria.Type)!;
            IncreaseStat(materia.BaseParam.Row, materia.Value[gearsetMateria.Grade]);
        }

        cp = Math.Min(cp, CalculateParamCap(item, ParamCP));
        craftsmanship = Math.Min(craftsmanship, CalculateParamCap(item, ParamCraftsmanship));
        control = Math.Min(control, CalculateParamCap(item, ParamControl));

        return new(cp, craftsmanship, control);
    }

    public static GearsetStats CalculateGearsetStats(GearsetItem[] gearsetItems) =>
        gearsetItems.Select(CalculateGearsetItemStats).Aggregate(BaseStats, (a, b) => new(a.CP + b.CP, a.Craftsmanship + b.Craftsmanship, a.Control + b.Control));

    public static GearsetStats CalculateGearsetCurrentStats()
    {
        var attributes = UIState.Instance()->PlayerState.Attributes;

        return new()
        {
            CP = attributes[ParamCP],
            Craftsmanship = attributes[ParamCraftsmanship],
            Control = attributes[ParamControl],
        };
    }

    public static CharacterStats CalculateCharacterStats(GearsetItem[] gearsetItems, int characterLevel, bool canUseManipulation) =>
        CalculateCharacterStats(CalculateGearsetStats(gearsetItems), gearsetItems, characterLevel, canUseManipulation);

    public static CharacterStats CalculateCharacterStats(GearsetStats gearsetStats, GearsetItem[] gearsetItems, int characterLevel, bool canUseManipulation) =>
        new()
        {
            CP = gearsetStats.CP,
            Craftsmanship = gearsetStats.Craftsmanship,
            Control = gearsetStats.Control,
            Level = characterLevel,
            CanUseManipulation = canUseManipulation,
            HasSplendorousBuff = gearsetItems.Any(IsSplendorousTool),
            IsSpecialist = gearsetItems.Any(IsSpecialistSoulCrystal),
            CLvl = CalculateCLvl(characterLevel),
        };

    public static bool IsItem(GearsetItem item, uint itemId) =>
        item.itemId == itemId;

    public static bool IsSpecialistSoulCrystal(GearsetItem item)
    {
        if (item.itemId == 0)
            return false;

        var luminaItem = LuminaSheets.ItemSheet.GetRow(item.itemId)!;
        //     Soul Crystal ItemUICategory                                          DoH Category
        return luminaItem.ItemUICategory.Row == 62 && luminaItem.ClassJobUse.Value!.ClassJobCategory.Row == 33;
    }

    public static bool IsSplendorousTool(GearsetItem item) =>
        LuminaSheets.ItemSheetEnglish.GetRow(item.itemId)!.Description.ToDalamudString().TextValue.Contains("Increases to quality are 1.75 times higher than normal when material condition is Good.", StringComparison.Ordinal);

    public static int CalculateCLvl(int level) =>
        (level > 0 && level <= 90) ?
            LevelToCLvlLUT[level - 1] :
            throw new ArgumentOutOfRangeException(nameof(level), level, "Level is out of range.");

    // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/24d0db2d9676f264edf53651b21005305267c84c/apps/client/src/app/modules/gearsets/materia.service.ts#L265
    private static int CalculateParamCap(Item item, uint paramId)
    {
        var ilvl = item.LevelItem.Value!;
        var param = LuminaSheets.BaseParamSheet.GetRow(paramId)!;

        var baseValue = paramId switch
        {
            ParamCP => ilvl.CP,
            ParamCraftsmanship => ilvl.Craftsmanship,
            ParamControl => ilvl.Control,
            _ => 0
        };
        // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/24d0db2d9676f264edf53651b21005305267c84c/apps/data-extraction/src/extractors/items.extractor.ts#L6
        var slotMod = item.EquipSlotCategory.Row switch
        {
            1 => param.OneHandWeaponPercent, // column 4
            2 => param.OffHandPercent,       // column 5
            3 => param.HeadPercent,          // ...
            4 => param.ChestPercent,
            5 => param.HandsPercent,
            6 => param.WaistPercent,
            7 => param.LegsPercent,
            8 => param.FeetPercent,
            9 => param.EarringPercent,
            10 => param.NecklacePercent,
            11 => param.BraceletPercent,
            12 => param.RingPercent,
            13 => param.TwoHandWeaponPercent,
            14 => param.OneHandWeaponPercent,
            15 => param.ChestHeadPercent,
            16 => param.ChestHeadLegsFeetPercent,
            17 => 0,
            18 => param.LegsFeetPercent,
            19 => param.HeadChestHandsLegsFeetPercent,
            20 => param.ChestLegsGlovesPercent,
            21 => param.ChestLegsFeetPercent,
            _ => 0
        };
        var roleMod = param.MeldParam[item.BaseParamModifier];

        // https://github.com/Caraxi/SimpleTweaksPlugin/pull/595
        var cap = (int)Math.Round((float)baseValue * slotMod / (roleMod * 10f), MidpointRounding.AwayFromZero);
        return cap == 0 ? int.MaxValue : cap;
    }

    private static GearsetMateria[] GetMaterias(ushort* types, byte* grades)
    {
        var materia = new GearsetMateria[5];
        for (var i = 0; i < 5; ++i)
            materia[i] = new(types[i], grades[i]);
        return materia;
    }
}
