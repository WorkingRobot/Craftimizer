using Craftimizer.Simulator;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ExdSheets.Sheets;
using System;
using System.Linq;
using Craftimizer.Plugin;

namespace Craftimizer.Utils;

public static unsafe class Gearsets
{
    public record struct GearsetStats(int CP, int Craftsmanship, int Control);
    public record struct GearsetMateria(ushort Type, ushort Grade);
    public record struct GearsetItem(uint ItemId, bool IsHq, GearsetMateria[] Materia);

    private static readonly GearsetStats BaseStats = new(180, 0, 0);

    public const int ParamCP = 11;
    public const int ParamCraftsmanship = 70;
    public const int ParamControl = 71;

    public static GearsetItem[] GetGearsetItems(InventoryContainer* container)
    {
        var items = new GearsetItem[(int)container->Size];
        for (var i = 0; i < container->Size; ++i)
        {
            var item = container->Items[i];
            items[i] = new(item.ItemId, item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality), GetMaterias(item.Materia, item.MateriaGrades));
        }
        return items;
    }

    public static GearsetItem[] GetGearsetItems(RaptureGearsetModule.GearsetEntry* entry)
    {
        var gearsetItems = entry->Items;
        var items = new GearsetItem[14];
        for (var i = 0; i < 14; ++i)
        {
            var item = gearsetItems[i];
            items[i] = new(item.ItemId % 1000000, item.ItemId > 1000000, GetMaterias(item.Materia, item.MateriaGrades));
        }
        return items;
    }

    public static GearsetStats CalculateGearsetItemStats(GearsetItem gearsetItem)
    {
        var item = LuminaSheets.ItemSheet.GetRow(gearsetItem.ItemId)!;

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
            IncreaseStat(statIncrease.First.RowId, statIncrease.Second);
        if (gearsetItem.IsHq)
            foreach (var statIncrease in item.BaseParamSpecial.Zip(item.BaseParamValueSpecial))
                IncreaseStat(statIncrease.First.RowId, statIncrease.Second);

        foreach (var gearsetMateria in gearsetItem.Materia)
        {
            if (gearsetMateria.Type == 0)
                continue;

            var materia = LuminaSheets.MateriaSheet.GetRow(gearsetMateria.Type)!;
            IncreaseStat(materia.BaseParam.RowId, materia.Value[gearsetMateria.Grade]);
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

    public static CharacterStats CalculateCharacterStats(GearsetItem[] gearsetItems, int characterLevel, bool canUseManipulation, bool checkDelineations) =>
        CalculateCharacterStats(CalculateGearsetStats(gearsetItems), gearsetItems, characterLevel, canUseManipulation, checkDelineations);

    public static CharacterStats CalculateCharacterStats(GearsetStats gearsetStats, GearsetItem[] gearsetItems, int characterLevel, bool canUseManipulation, bool checkDelineations) =>
        new()
        {
            CP = gearsetStats.CP,
            Craftsmanship = gearsetStats.Craftsmanship,
            Control = gearsetStats.Control,
            Level = characterLevel,
            CanUseManipulation = canUseManipulation,
            HasSplendorousBuff = gearsetItems.Any(IsSplendorousTool),
            IsSpecialist = gearsetItems.Any(IsSpecialistSoulCrystal) && (!checkDelineations || HasDelineations()),
        };

    public static bool HasDelineations() =>
        InventoryManager.Instance()->GetInventoryItemCount(28724) > 0;

    public static bool IsItem(GearsetItem item, uint itemId) =>
        item.ItemId == itemId;

    public static bool IsSpecialistSoulCrystal(GearsetItem item)
    {
        if (item.ItemId == 0)
            return false;

        var luminaItem = LuminaSheets.ItemSheet.GetRow(item.ItemId)!;
        //     Soul Crystal ItemUICategory                                           DoH Category
        return luminaItem.ItemUICategory.RowId == 62 && luminaItem.ClassJobUse.Value.ClassJobCategory.RowId == 33;
    }

    public static bool IsSplendorousTool(GearsetItem item) =>
        LuminaSheets.ItemSheetEnglish.GetRow(item.ItemId).Description.ExtractText().Contains("Increases to quality are 1.75 times higher than normal when material condition is Good.", StringComparison.Ordinal);

    // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/24d0db2d9676f264edf53651b21005305267c84c/apps/client/src/app/modules/gearsets/materia.service.ts#L265
    private static int CalculateParamCap(Item item, uint paramId)
    {
        var ilvl = item.LevelItem.Value;
        var param = LuminaSheets.BaseParamSheet.GetRow(paramId)!;

        var baseValue = paramId switch
        {
            ParamCP => ilvl.CP,
            ParamCraftsmanship => ilvl.Craftsmanship,
            ParamControl => ilvl.Control,
            _ => 0
        };
        // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/24d0db2d9676f264edf53651b21005305267c84c/apps/data-extraction/src/extractors/items.extractor.ts#L6
        var slotMod = item.EquipSlotCategory.RowId switch
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

    private static GearsetMateria[] GetMaterias(ReadOnlySpan<ushort> types, ReadOnlySpan<byte> grades)
    {
        var materia = new GearsetMateria[5];
        for (var i = 0; i < 5; ++i)
            materia[i] = new(types[i], grades[i]);
        return materia;
    }
}
