using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Craftimizer.Plugin.Windows;

public unsafe class CraftingLog : Window
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration
      | ImGuiWindowFlags.NoInputs
      | ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoFocusOnAppearing
      | ImGuiWindowFlags.NoNavFocus;

    private AddonRecipeNote* Addon { get; set; }
    private RecipeNote* State { get; set; }
    private ushort SelectedRecipeId { get; set; }
    private Recipe SelectedRecipe { get; set; } = null!;

    public CraftingLog() : base("RecipeNoteHelper", WindowFlags, true)
    {
        IsOpen = true;
    }

    public override void Draw()
    {
        ImGui.Text("Hai!! :3333");

        var inst = RaptureGearsetModule.Instance();

        for (var i = 0; i < 100; i++)
        {
            var gearset = inst->Gearset[i];
            if (gearset == null)
                continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;
            if (gearset->ID != i)
                continue;
            var job = LuminaSheets.ClassJobSheet.GetRow(gearset->ClassJob)!;
            if (job.ClassJobCategory.Row != 33) // DoH
                continue;
            if (job.DohDolJobIndex != SelectedRecipe.CraftType.Row)
                continue;
            ImGui.Text($"Supported Gearset: {gearset->ID + 1} {Marshal.PtrToStringUTF8((nint)gearset->Name)}");
            var stats = CalculateGearsetStats(gearset);
            ImGui.Text($"{stats.CP} CP, {stats.Craftsmanship} Craftsmanship, {stats.Control} Control");
        }


        ShowCurrentGearInfo();
    }

    private void ShowCurrentGearInfo()
    {
        if (Service.ClientState.LocalPlayer == null)
            return;

        var classJob = Service.ClientState.LocalPlayer.ClassJob.Id;

        var job = LuminaSheets.ClassJobSheet.GetRow(classJob)!;
        if (job.ClassJobCategory.Row != 33) // DoH
            return;
        if (job.DohDolJobIndex != SelectedRecipe.CraftType.Row)
            return;

        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            return;

        (int CP, int Craftsmanship, int Control) stats = (180, 0, 0);
        for (var i = 0; i < container->Size; ++i)
        {
            var itemStats = CalculateGearsetItemStats(container->Items[i]);
            stats.CP += itemStats.CP;
            stats.Craftsmanship += itemStats.Craftsmanship;
            stats.Control += itemStats.Control;
        }
        ImGui.Text($"Currently Equipped");
        ImGui.Text($"{stats.CP} CP, {stats.Craftsmanship} Craftsmanship, {stats.Control} Control");
    }

    private static readonly (int CP, int Craftsmanship, int Control) BaseStats = (180, 0, 0);

    private static (int CP, int Craftsmanship, int Control) CalculateGearsetStats(RaptureGearsetModule.GearsetEntry* entry)
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
        return stats.Aggregate((a, b) => (a.CP + b.CP, a.Craftsmanship + b.Craftsmanship, a.Control + b.Control));
    }

    private const int ParamCP = 11;
    private const int ParamCraftsmanship = 70;
    private const int ParamControl = 71;

    private static (int CP, int Craftsmanship, int Control) CalculateGearsetItemStats(InventoryItem item) =>
        CalculateGearsetItemStats(item.ItemID, item.Flags.HasFlag(InventoryItem.ItemFlags.HQ), item.Materia, item.MateriaGrade);

    private static (int CP, int Craftsmanship, int Control) CalculateGearsetItemStats(RaptureGearsetModule.GearsetItem item) =>
        CalculateGearsetItemStats(item.ItemID % 1000000, item.ItemID > 1000000, item.Materia, item.MateriaGrade);

    private static (int CP, int Craftsmanship, int Control) CalculateGearsetItemStats(uint itemId, bool isHq, ushort* materiaTypes, byte* materiaGrades)
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

        foreach(var statIncrease in item.UnkData59)
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

        cp = Math.Min(cp, CalculateMateriaCap(item, ParamCP));
        craftsmanship = Math.Min(craftsmanship, CalculateMateriaCap(item, ParamCraftsmanship));
        control = Math.Min(control, CalculateMateriaCap(item, ParamControl));

        return (cp, craftsmanship, control);
    }

    // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/24d0db2d9676f264edf53651b21005305267c84c/apps/client/src/app/modules/gearsets/materia.service.ts#L265
    private static int CalculateMateriaCap(Item item, int paramId)
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

    public override bool DrawConditions()
    {
        Addon = (AddonRecipeNote*)Service.GameGui.GetAddonByName("RecipeNote");

        if (Addon == null)
            return false;

        if (Addon->AtkUnitBase.WindowNode == null)
            return false;

        State = RecipeNote.Instance();

        var list = State->RecipeList;
        
        if (list == null)
            return false;

        var recipeEntry = list->SelectedRecipe;

        if (recipeEntry == null)
            return false;

        SelectedRecipeId = recipeEntry->RecipeId;

        var recipe = LuminaSheets.RecipeSheet.GetRow(SelectedRecipeId);

        if (recipe == null)
            return false;

        SelectedRecipe = recipe;

        if (!Addon->Unk258->IsVisible)
            return false;

        return base.DrawConditions();
    }

    public override unsafe void PreDraw()
    {
        ref var unit = ref Addon->AtkUnitBase;
        var scale = unit.Scale;
        var pos = new Vector2(unit.X, unit.Y);
        var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

        var node = (AtkResNode*)Addon->Unk458; // unit.GetNodeById(59);
        var nodeParent = Addon->Unk258; // unit.GetNodeById(57);

        //for (var i = 544; i <= 1960; i += 8)
        //{
        //    if (Marshal.ReadIntPtr((nint)Addon, i) == (nint)nodeParent)
        //    {
        //        PluginLog.LogDebug($"{i}");
        //    }
        //}

        Position = pos + new Vector2(size.X, (nodeParent->Y + node->Y) * scale);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(-1),
            MaximumSize = new(10000, 10000)
        };

        base.PreDraw();
    }
}
