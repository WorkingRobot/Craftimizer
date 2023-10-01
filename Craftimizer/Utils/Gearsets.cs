using Craftimizer.Simulator;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;

namespace Craftimizer.Plugin.Utils;
public static unsafe class Gearsets
{
    public record struct GearsetStats(int CP, int Craftsmanship, int Control);
    public record struct GearsetMateria(ushort Type, ushort Grade);
    public record struct GearsetItem(uint itemId, bool isHq, GearsetMateria[] materia);

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
            items[i] = new(item.ItemID, item.Flags.HasFlag(InventoryItem.ItemFlags.HQ), GetMaterias(item.Materia, item.MateriaGrade));
        }
        return items;
    }

    public static GearsetItem[] GetGearsetItems(RaptureGearsetModule.GearsetEntry* entry)
    {
        var gearsetItems = new Span<RaptureGearsetModule.GearsetItem>(entry->ItemsData, 14);
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

        void IncreaseStat(int baseParam, int amount)
        {
            if (baseParam == ParamCP)
                cp += amount;
            else if (baseParam == ParamCraftsmanship)
                craftsmanship += amount;
            else if (baseParam == ParamControl)
                control += amount;
        }

        foreach (var statIncrease in item.UnkData59)
            IncreaseStat(statIncrease.BaseParam, statIncrease.BaseParamValue);
        if (gearsetItem.isHq)
            foreach (var statIncrease in item.UnkData73)
                IncreaseStat(statIncrease.BaseParamSpecial, statIncrease.BaseParamValueSpecial);

        foreach (var gearsetMateria in gearsetItem.materia)
        {
            if (gearsetMateria.Type == 0)
                continue;

            var materia = LuminaSheets.MateriaSheet.GetRow(gearsetMateria.Type)!;
            IncreaseStat((int)materia.BaseParam.Row, materia.Value[gearsetMateria.Grade]);
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

    public static int CalculateCLvl(int characterLevel) =>
        characterLevel <= 80
        ? LuminaSheets.ParamGrowSheet.GetRow((uint)characterLevel)!.CraftingLevel
        : (int)LuminaSheets.RecipeLevelTableSheet.First(r => r.ClassJobLevel == characterLevel).RowId;

    // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/24d0db2d9676f264edf53651b21005305267c84c/apps/client/src/app/modules/gearsets/materia.service.ts#L265
    private static int CalculateParamCap(Item item, int paramId)
    {
        var ilvl = item.LevelItem.Value!;
        var param = LuminaSheets.BaseParamSheet.GetRow((uint)paramId)!;

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
            1 => param.oneHWpnPct,
            2 => param.OHPct,
            3 => param.HeadPct,
            4 => param.ChestPct,
            5 => param.HandsPct,
            6 => param.WaistPct,
            7 => param.LegsPct,
            8 => param.FeetPct,
            9 => param.EarringPct,
            10 => param.NecklacePct,
            11 => param.BraceletPct,
            12 => param.RingPct,
            13 => param.twoHWpnPct,
            14 => param.oneHWpnPct,
            15 => param.ChestHeadPct,
            16 => param.ChestHeadLegsFeetPct,
            18 => param.LegsFeetPct,
            19 => param.HeadChestHandsLegsFeetPct,
            20 => param.ChestLegsGlovesPct,
            21 => param.ChestLegsFeetPct,
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
