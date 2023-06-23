using Craftimizer.Plugin.Utils;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud;
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
    private ushort RecipeId { get; set; }
    private Recipe Recipe { get; set; } = null!;
    private RecipeInfo RecipeInfo { get; set; } = null!;

    private ClassJob RecipeClassJob => (ClassJob)Recipe.CraftType.Row;
    private short RecipeCharacterLevel => PlayerState.Instance()->ClassJobLevelArray[RecipeClassJob.GetClassJobIndex()];
    private bool RecipeCanUseManipulation => ActionManager.CanUseActionOnTarget(ActionType.Manipulation.GetId(RecipeClassJob), (GameObject*)Service.ClientState.LocalPlayer!.Address);
    private RecipeLevelTable RecipeTable => Recipe.RecipeLevelTable.Value!;

    private int startingQuality;

    public CraftingLog() : base("RecipeNoteHelper", WindowFlags, true)
    {
        IsOpen = true;
    }

    public override void Draw()
    {
        if (Service.ClientState.LocalPlayer == null)
            return;

        DrawCraftInfo();

        DrawGearsets();
    }

    void DrawCraftInfo()
    {
        DrawRecipeInfo();
        DrawCharacterInfo();

        DrawCraftActions();
    }

    void DrawRecipeInfo()
    {
        var s = new StringBuilder();
        s.AppendLine($"{RecipeClassJob.GetName()} {new string('★', RecipeTable.Stars)}");
        s.AppendLine($"Level {RecipeTable.ClassJobLevel} (RLvl {RecipeInfo.RLvl})");
        s.AppendLine($"Durability: {RecipeInfo.MaxDurability}");
        s.AppendLine($"Progress: {RecipeInfo.MaxProgress}");
        s.AppendLine($"Quality: {RecipeInfo.MaxQuality}");
        s.AppendLine($"Starting Quality: {startingQuality}");
        ImGui.Text(s.ToString());
    }

    void DrawCharacterInfo()
    {
        var classJob = (byte)Service.ClientState.LocalPlayer!.ClassJob.Id;

        if (!ClassJobUtils.IsClassJob(classJob, RecipeClassJob))
        {
            ImGui.Text("Your current class cannot craft this recipe.");
            return;
        }

        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            return;

        var stats = Gearsets.CalculateCharacterStats(container, RecipeCharacterLevel, RecipeCanUseManipulation);

        var s = new StringBuilder();
        s.AppendLine($"{RecipeClassJob.GetName()}");
        s.AppendLine($"Level {stats.Level} (CLvl {stats.CLvl})");
        s.AppendLine($"Craftsmanship {stats.Craftsmanship}");
        s.AppendLine($"Control {stats.Control}");
        s.AppendLine($"CP {stats.CP}");
        if (stats.IsSpecialist)
            s.AppendLine($" + Specialist");
        if (stats.HasSplendorousBuff)
            s.AppendLine($" + Splendorous");
        ImGui.Text(s.ToString());
    }

    void DrawCraftActions()
    {
        ImGui.Button("Open Simulator");
        ImGui.Button("Generate a new macro");
    }

    void DrawGearsets()
    {
        ImGui.Text("Available Gearsets");

        var inst = RaptureGearsetModule.Instance();

        for (var i = 0; i < 100; i++)
        {
            var gearset = inst->Gearset[i];
            if (gearset == null)
                continue;
            if (gearset->ID != i)
                continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;

            if (!ClassJobUtils.IsClassJob(gearset->ClassJob, RecipeClassJob))
                continue;

            var stats = Gearsets.CalculateCharacterStats(gearset, RecipeCharacterLevel, RecipeCanUseManipulation);
            var gearsetId = gearset->ID + 1;

            var s = new StringBuilder();
            s.AppendLine($"{SafeMemory.ReadString((nint)gearset->Name, 47)} ({gearsetId})");
            s.AppendLine($"Level {stats.Level} (CLvl {stats.CLvl})");
            s.AppendLine($"Craftsmanship {stats.Craftsmanship}");
            s.AppendLine($"Control {stats.Control}");
            s.AppendLine($"CP {stats.CP}");
            if (stats.IsSpecialist)
                s.AppendLine($" + Specialist");
            if (stats.HasSplendorousBuff)
                s.AppendLine($" + Splendorous");
            ImGui.Text(s.ToString());
            ImGui.SameLine();
            if (ImGuiComponents.IconButton($"SwapGearset{gearsetId}", FontAwesomeIcon.SyncAlt))
                Chat.SendMessage($"/gearset change {gearsetId}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Swap to gearset {gearsetId}");
        }
    }

    void OnNewRecipe()
    {
        startingQuality = 0;
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

        if (RecipeId != recipeEntry->RecipeId)
            OnNewRecipe();

        RecipeId = recipeEntry->RecipeId;

        var recipe = LuminaSheets.RecipeSheet.GetRow(RecipeId);

        if (recipe == null)
            return false;

        Recipe = recipe;

        RecipeInfo = SimulatorWindow.CreateRecipeInfo(Recipe);

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
