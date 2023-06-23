using Craftimizer.Simulator;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;

namespace Craftimizer.Plugin.Utils;
internal static unsafe class Gearsets
{
    private static readonly (int CP, int Craftsmanship, int Control, bool HasSplendorous, bool HasSpecialist) BaseStats = (180, 0, 0, false, false);

    private const int ParamCP = 11;
    private const int ParamCraftsmanship = 70;
    private const int ParamControl = 71;

    public static CharacterStats CalculateCharacterStats(InventoryContainer* container, int characterLevel, bool canUseManipulation)
    {
        var stats = CalculateGearsetStats(container);
        return new CharacterStats
        {
            CP = stats.CP,
            Craftsmanship = stats.Craftsmanship,
            Control = stats.Control,
            Level = characterLevel,
            CanUseManipulation = canUseManipulation,
            HasSplendorousBuff = stats.HasSplendorous,
            IsSpecialist = stats.HasSpecialist,
            CLvl = CalculateCLvl(characterLevel),
        };
    }

    public static CharacterStats CalculateCharacterStats(RaptureGearsetModule.GearsetEntry* entry, int characterLevel, bool canUseManipulation)
    {
        var stats = CalculateGearsetStats(entry);
        return new CharacterStats
        {
            CP = stats.CP,
            Craftsmanship = stats.Craftsmanship,
            Control = stats.Control,
            Level = characterLevel,
            CanUseManipulation = canUseManipulation,
            HasSplendorousBuff = stats.HasSplendorous,
            IsSpecialist = stats.HasSpecialist,
            CLvl = CalculateCLvl(characterLevel),
        };
    }

    private static (int CP, int Craftsmanship, int Control, bool HasSplendorous, bool HasSpecialist) CalculateGearsetStats(InventoryContainer* container)
    {
        var stats = BaseStats;
        for (var i = 0; i < container->Size; ++i)
        {
            var itemStats = CalculateGearsetItemStats(container->Items[i]);
            stats.CP += itemStats.CP;
            stats.Craftsmanship += itemStats.Craftsmanship;
            stats.Control += itemStats.Control;
            stats.HasSplendorous = stats.HasSplendorous || itemStats.HasSplendorous;
            stats.HasSpecialist = stats.HasSpecialist || itemStats.HasSpecialist;
        }
        return stats;
    }

    private static (int CP, int Craftsmanship, int Control, bool HasSplendorous, bool HasSpecialist) CalculateGearsetStats(RaptureGearsetModule.GearsetEntry* entry)
    {
        var stats = new[]
        {
            BaseStats,
            CalculateGearsetItemStats(entry->MainHand),
            CalculateGearsetItemStats(entry->OffHand),
            CalculateGearsetItemStats(entry->Head),
            CalculateGearsetItemStats(entry->Body),
            CalculateGearsetItemStats(entry->Hands),
            // CalculateGearsetItemStats(entry->Belt),
            CalculateGearsetItemStats(entry->Legs),
            CalculateGearsetItemStats(entry->Feet),
            CalculateGearsetItemStats(entry->Ears),
            CalculateGearsetItemStats(entry->Neck),
            CalculateGearsetItemStats(entry->Wrists),
            CalculateGearsetItemStats(entry->RingRight),
            CalculateGearsetItemStats(entry->RightLeft),
            CalculateGearsetItemStats(entry->SoulStone),
        };
        return stats.Aggregate((a, b) => (a.CP + b.CP, a.Craftsmanship + b.Craftsmanship, a.Control + b.Control, a.HasSplendorous || b.HasSplendorous, a.HasSpecialist || b.HasSpecialist));
    }

    private static (int CP, int Craftsmanship, int Control, bool HasSplendorous, bool HasSpecialist) CalculateGearsetItemStats(InventoryItem item) =>
        CalculateGearsetItemStats(item.ItemID, item.Flags.HasFlag(InventoryItem.ItemFlags.HQ), item.Materia, item.MateriaGrade);

    private static (int CP, int Craftsmanship, int Control, bool HasSplendorous, bool HasSpecialist) CalculateGearsetItemStats(RaptureGearsetModule.GearsetItem item) =>
        CalculateGearsetItemStats(item.ItemID % 1000000, item.ItemID > 1000000, item.Materia, item.MateriaGrade);

    private static (int CP, int Craftsmanship, int Control, bool HasSplendorous, bool HasSpecialist) CalculateGearsetItemStats(uint itemId, bool isHq, ushort* materiaTypes, byte* materiaGrades)
    {
        var item = LuminaSheets.ItemSheet.GetRow(itemId)!;

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

        if (isHq)
        {
            foreach (var statIncrease in item.UnkData73)
                IncreaseStat(statIncrease.BaseParamSpecial, statIncrease.BaseParamValueSpecial);
        }
        for (var i = 0; i < 5; ++i)
        {
            if (materiaTypes[i] == 0)
                continue;
            var materia = LuminaSheets.MateriaSheet.GetRow(materiaTypes[i])!;

            IncreaseStat((int)materia.BaseParam.Row, materia.Value[materiaGrades[i]]);
        }

        cp = Math.Min(cp, CalculateParamCap(item, ParamCP));
        craftsmanship = Math.Min(craftsmanship, CalculateParamCap(item, ParamCraftsmanship));
        control = Math.Min(control, CalculateParamCap(item, ParamControl));

        return (cp, craftsmanship, control, IsSpecialistSoulCrystal(item), IsSplendorousTool(itemId));
    }

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

    private static bool IsSpecialistSoulCrystal(Item item) =>
        // Soul Crystal ItemUICategory                     DoH Category
        item.ItemUICategory.Row != 62 && item.ClassJobUse.Value!.ClassJobCategory.Row == 33;

    private static bool IsSplendorousTool(uint itemId) =>
        LuminaSheets.ItemSheetEnglish.GetRow(itemId)!.Description.ToDalamudString().TextValue.Contains("Increases to quality are 1.75 times higher than normal when material condition is Good.", StringComparison.Ordinal);
        // 38737 <= itemId && itemId <= 38744;

    public static int CalculateCLvl(int characterLevel) =>
        characterLevel <= 80
        ? LuminaSheets.ParamGrowSheet.GetRow((uint)characterLevel)!.CraftingLevel
        : (int)LuminaSheets.RecipeLevelTableSheet.First(r => r.ClassJobLevel == characterLevel).RowId;
}
