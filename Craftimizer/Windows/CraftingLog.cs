using Craftimizer.Plugin.Utils;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Plugin.Windows;

public unsafe class CraftingLog : Window
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration
      | ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoFocusOnAppearing
      | ImGuiWindowFlags.NoNavFocus;

    private AddonRecipeNote* Addon { get; set; }
    private RecipeNote* State { get; set; }
    private ushort SelectedRecipeId { get; set; }
    private Recipe SelectedRecipe { get; set; } = null!;
    private RecipeInfo SelectedRecipeInfo { get; set; } = null!;

    public CraftingLog() : base("RecipeNoteHelper", WindowFlags, true)
    {
        IsOpen = true;
    }

    public override void Draw()
    {
        var inst = RaptureGearsetModule.Instance();

        if (Service.ClientState.LocalPlayer == null)
            return;

        var recipeClassJob = (ClassJob)SelectedRecipe.CraftType.Row;

        var characterLevel = PlayerState.Instance()->ClassJobLevelArray[recipeClassJob.GetClassJobIndex()];
        var canUseManipulation = ActionManager.CanUseActionOnTarget(ActionType.Manipulation.GetId(recipeClassJob), (GameObject*)Service.ClientState.LocalPlayer.Address);

        for (var i = 0; i < 100; i++)
        {
            var gearset = inst->Gearset[i];
            if (gearset == null)
                continue;
            if (gearset->ID != i)
                continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;

            if (!ClassJobUtils.IsClassJob(gearset->ClassJob, recipeClassJob))
                continue;

            var stats = Gearsets.CalculateCharacterStats(gearset, characterLevel, canUseManipulation);
            ImGui.Text($"Gearset: {gearset->ID + 1} {Marshal.PtrToStringUTF8((nint)gearset->Name)}");
            ImGui.SameLine();
            if (ImGuiComponents.IconButton($"SwapGearset{gearset->ID}", FontAwesomeIcon.SyncAlt))
                Chat.SendMessage($"/gearset change {gearset->ID + 1}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Swap to gearset {gearset->ID + 1}");
            ImGui.Text($"{stats}");
        }

        {
            var classJob = (byte)Service.ClientState.LocalPlayer.ClassJob.Id;

            if (!ClassJobUtils.IsClassJob(classJob, recipeClassJob))
                return;

            var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            if (container == null)
                return;

            var stats = Gearsets.CalculateCharacterStats(container, characterLevel, canUseManipulation);
            ImGui.Text($"Currently Equipped");
            ImGui.Text($"{stats}");
        }
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

        SelectedRecipeInfo = SimulatorWindow.CreateRecipeInfo(SelectedRecipe);

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
