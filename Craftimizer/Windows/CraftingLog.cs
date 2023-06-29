using Craftimizer.Plugin.Utils;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
      | ImGuiWindowFlags.NoSavedSettings
      | ImGuiWindowFlags.NoFocusOnAppearing
      | ImGuiWindowFlags.NoNavFocus;

    private const int LeftSideWidth = 350;

    // If relative, increase stat by Value's % (rounded down), and cap increase to Max
    // If not relative, increase stat by Value, and ignore Max
    [StructLayout(LayoutKind.Auto)]
    private record struct FoodStat(bool IsRelative, sbyte Value, short Max, sbyte ValueHQ, short MaxHQ);
    private sealed record Food(Item Item, string Name, FoodStat? Craftsmanship, FoodStat? Control, FoodStat? CP);

    private static Food[] FoodItems { get; }
    private static Food[] MedicineItems { get; }
    private static Random Random { get; }

    private TimeSpan FrameTime { get; set; }
    private Stopwatch Stopwatch { get; } = new();

    // Set in DrawConditions
    private AddonRecipeNote* Addon { get; set; }
    private RecipeNote* State { get; set; }
    private ushort RecipeId { get; set; }
    private Recipe Recipe { get; set; } = null!;

    // Set in CalculateRecipeStats (in DrawConditions)
    private RecipeLevelTable RecipeTable { get; set; } = null!;
    private RecipeInfo RecipeInfo { get; set; } = null!;
    private ClassJob RecipeClassJob { get; set; }
    private short RecipeCharacterLevel { get; set; }
    private bool RecipeCanUseManipulation { get; set; }
    private int RecipeHQIngredientCount { get; set; }
    private int RecipeMaxStartingQuality { get; set; }

    // Set in CalculateCharacterStats (in PreDraw)
    private Gearsets.GearsetItem[] CharacterEquipment { get; set; } = null!;
    private CharacterStats CharacterStatsNoConsumable { get; set; } = null!;
    private Gearsets.GearsetStats CharacterConsumableBonus { get; set; }
    private CharacterStats CharacterStatsConsumable { get; set; } = null!;
    private CannotCraftReason CharacterCannotCraftReason { get; set; }
    private SimulationInput CharacterSimulationInput { get; set; } = null!;
    
    // Set in UI
    private int QualityNotches { get; set; }
    private int StartingQuality => RecipeHQIngredientCount == 0 ? 0 : (int)((float)QualityNotches * RecipeMaxStartingQuality / RecipeHQIngredientCount);

    private Food? SelectedFood { get; set; }
    private bool SelectedFoodHQ { get; set; }

    private Food? SelectedMedicine { get; set; }
    private bool SelectedMedicineHQ { get; set; }

    static CraftingLog()
    {
        var foods = new List<Food>();
        var medicines = new List<Food>();
        foreach (var item in LuminaSheets.ItemSheet)
        {
            var isFood = item.ItemUICategory.Row == 46;
            var isMedicine = item.ItemUICategory.Row == 44;
            if (!isFood && !isMedicine)
                continue;

            if (item.ItemAction.Value == null)
                continue;

            if (!(item.ItemAction.Value.Type is 844 or 845 or 846))
                continue;

            var itemFood = LuminaSheets.ItemFoodSheet.GetRow(item.ItemAction.Value.Data[1]);
            if (itemFood == null)
                continue;

            FoodStat? craftsmanship = null, control = null, cp = null;
            foreach (var stat in itemFood.UnkData1)
            {
                if (stat.BaseParam == 0)
                    continue;
                var foodStat = new FoodStat(stat.IsRelative, stat.Value, stat.Max, stat.ValueHQ, stat.MaxHQ);
                switch (stat.BaseParam)
                {
                    case Gearsets.ParamCraftsmanship: craftsmanship = foodStat; break;
                    case Gearsets.ParamControl: control = foodStat; break;
                    case Gearsets.ParamCP: cp = foodStat; break;
                    default: continue;
                }
            }

            if (craftsmanship != null || control != null || cp != null)
            {
                var food = new Food(item, item.Name.ToDalamudString().TextValue ?? $"Unknown ({item.RowId})", craftsmanship, control, cp);
                if (isFood)
                    foods.Add(food);
                if (isMedicine)
                    medicines.Add(food);
            }
        }
        foods.Sort((a, b) => b.Item.LevelItem.Row.CompareTo(a.Item.LevelItem.Row));
        medicines.Sort((a, b) => b.Item.LevelItem.Row.CompareTo(a.Item.LevelItem.Row));
        FoodItems = foods.ToArray();
        MedicineItems = medicines.ToArray();

        Random = new();
    }

    public CraftingLog() : base("RecipeNoteHelper", WindowFlags, true)
    {
        Service.WindowSystem.AddWindow(this);

        IsOpen = true;
    }

    private void CalculateRecipeStats()
    {
        RecipeTable = Recipe.RecipeLevelTable.Value!;
        RecipeInfo = CreateRecipeInfo(Recipe);
        RecipeClassJob = (ClassJob)Recipe.CraftType.Row;
        RecipeCharacterLevel = PlayerState.Instance()->ClassJobLevelArray[RecipeClassJob.GetClassJobIndex()];
        RecipeCanUseManipulation = ActionManager.CanUseActionOnTarget(ActionType.Manipulation.GetId(RecipeClassJob), (GameObject*)Service.ClientState.LocalPlayer!.Address);
        RecipeHQIngredientCount = Recipe.UnkData5
            .Where(i =>
                i != null &&
                i.ItemIngredient != 0 &&
                (LuminaSheets.ItemSheet.GetRow((uint)i.ItemIngredient)?.CanBeHq ?? false)
            ).Sum(i => i.AmountIngredient);
        RecipeMaxStartingQuality = (int)Math.Floor(Recipe.MaterialQualityFactor * RecipeInfo.MaxQuality / 100f);
    }

    private void CalculateCharacterStats()
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            return;

        CharacterEquipment = Gearsets.GetGearsetItems(container);
        CharacterStatsNoConsumable = Gearsets.CalculateCharacterStats(CharacterEquipment, RecipeCharacterLevel, RecipeCanUseManipulation);
        CharacterConsumableBonus = CalculateConsumableBonus(CharacterStatsNoConsumable);
        CharacterStatsConsumable = CharacterStatsNoConsumable with
        {
            Craftsmanship = CharacterStatsNoConsumable.Craftsmanship + CharacterConsumableBonus.Craftsmanship,
            Control = CharacterStatsNoConsumable.Control + CharacterConsumableBonus.Control,
            CP = CharacterStatsNoConsumable.CP + CharacterConsumableBonus.CP,
        };
        CharacterCannotCraftReason = Service.Configuration.OverrideUncraftability ? CannotCraftReason.OK : CanCraftRecipe(CharacterEquipment, CharacterStatsConsumable);
        
        if (CharacterCannotCraftReason == CannotCraftReason.OK)
            CharacterSimulationInput = new(CharacterStatsConsumable, RecipeInfo, StartingQuality, Random);
    }

    public override void Draw()
    {
        ImGui.BeginTable("craftlog", 2, ImGuiTableFlags.BordersInnerV);

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, LeftSideWidth);
        ImGui.TableNextColumn();
        DrawCraftInfo();

        ImGui.TableNextColumn();
        DrawGearsets();

        ImGui.EndTable();

        ImGui.TextUnformatted($"{FrameTime.TotalMilliseconds:0.00}ms");
    }

    private void DrawCraftInfo()
    {
        ImGui.BeginTable("craftinfo", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame);

        ImGui.TableNextColumn();
        DrawRecipeInfo();

        ImGui.TableNextColumn();
        DrawCharacterInfo();
        ImGui.EndTable();

        ImGui.Separator();

        DrawCraftParameters();
        DrawMacros();
    }

    private void DrawRecipeInfo()
    {
        var s = new StringBuilder();
        s.AppendLine($"{RecipeClassJob.GetName()} {new string('★', RecipeTable.Stars)}");
        s.AppendLine($"Level {RecipeTable.ClassJobLevel} (RLvl {RecipeInfo.RLvl})");
        s.AppendLine($"Durability: {RecipeInfo.MaxDurability}");
        s.AppendLine($"Progress: {RecipeInfo.MaxProgress}");
        s.AppendLine($"Quality: {RecipeInfo.MaxQuality}");
        ImGui.Text(s.ToString());
    }

    private void DrawCharacterInfo()
    {
        if (CharacterCannotCraftReason != CannotCraftReason.OK)
        {
            ImGui.TextWrapped(GetCannotCraftReasonText(CharacterCannotCraftReason));
            return;
        }

        ImGui.Text(GetCharacterStatsText(CharacterStatsConsumable));
    }

    private void DrawCraftParameters()
    {
        ImGui.BeginDisabled(RecipeHQIngredientCount == 0);
        var qualityNotches = QualityNotches;
        ImGui.SetNextItemWidth(LeftSideWidth - 115);
        if (ImGui.SliderInt("Starting Quality", ref qualityNotches, 0, RecipeHQIngredientCount, StartingQuality.ToString(), ImGuiSliderFlags.NoInput | ImGuiSliderFlags.AlwaysClamp))
            QualityNotches = qualityNotches;
        ImGui.EndDisabled();

        ImGui.BeginTable("craftfood", 2, ImGuiTableFlags.BordersInnerV);
        
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, LeftSideWidth - 120);
        ImGui.TableNextColumn();

        if (ImGui.BeginCombo("Food", SelectedFood?.Name ?? "None"))
        {
            if (ImGui.Selectable("None", SelectedFood == null))
                SelectedFood = null;

            foreach (var food in FoodItems)
                if (ImGui.Selectable(food.Name, food == SelectedFood))
                    SelectedFood = food;

            ImGui.EndCombo();
        }

        if (ImGui.BeginCombo("Medicine", SelectedMedicine?.Name ?? "None"))
        {
            if (ImGui.Selectable("None", SelectedMedicine == null))
                SelectedMedicine = null;

            foreach (var food in MedicineItems)
                if (ImGui.Selectable(food.Name, food == SelectedMedicine))
                    SelectedMedicine = food;

            ImGui.EndCombo();
        }

        ImGui.TableNextColumn();

        var s = new StringBuilder();
        s.AppendLine($"+{CharacterConsumableBonus.Craftsmanship} Craftsmanship");
        s.AppendLine($"+{CharacterConsumableBonus.Control} Control");
        s.AppendLine($"+{CharacterConsumableBonus.CP} CP");
        ImGui.Text(s.ToString());

        ImGui.EndTable();
    }

    private void DrawMacros()
    {
        var padding = ImGui.GetStyle().FramePadding;
        var itemPadding = ImGui.GetStyle().ItemInnerSpacing;

        var fontSize = ImGui.GetFontSize();
        var height = fontSize + (padding.Y * 2);
        var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, height);
        var infoColWidth = SimulatorWindow.TooltipProgressBarSize.X;
        var infoButtonCount = 3;
        var infoButtonWidth = (infoColWidth - ImGui.GetStyle().ItemSpacing.X * (infoButtonCount - 1)) / infoButtonCount;
        var infoButtonSize = new Vector2(infoButtonWidth, height);
        var actionColWidth = width - infoColWidth - ImGui.GetStyle().FramePadding.X * 2;
        var actionCount = 6;
        var actionSize = new Vector2((actionColWidth - (ImGui.GetStyle().ItemSpacing.X * (actionCount - 1))) / actionCount);

        if (ImGui.Button("Open Simulator", size))
            OpenSimulatorWindow(null);
        ImGui.SameLine();
        ImGui.Button("Generate a new macro", size);

        ImGui.BeginTable("macrotable", 2, ImGuiTableFlags.BordersInner);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, infoColWidth);
        ImGui.TableSetupColumn("");
        var simulation = new SimulatorNoRandom(new(CharacterSimulationInput));
        for (var i = 0; i < Service.Configuration.Macros.Count; ++i)
        {
            var macro = Service.Configuration.Macros[i];
            ImGui.PushID(i);
            ImGui.TableNextRow();

            SimulationState? state = null;
            if (CharacterCannotCraftReason == CannotCraftReason.OK) {
                state = new(CharacterSimulationInput);
                foreach (var action in macro.Actions)
                    (_, state) = simulation.Execute(state.Value, action);
            }

            ImGui.TableNextColumn();
            ImGui.TextWrapped(macro.Name);
            if (state.HasValue)
                SimulatorWindow.DrawAllProgressTooltips(state!.Value);

            if (ImGuiUtils.IconButtonSized(FontAwesomeIcon.Copy, infoButtonSize))
                CopyMacroToClipboard(macro);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Copy macro to clipboard\nHold Shift to exclude wait modifiers");
            ImGui.SameLine();
            if (ImGuiUtils.IconButtonSized(FontAwesomeIcon.ShareSquare, infoButtonSize))
                OpenSimulatorWindow(macro);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Open macro in simulator");
            ImGui.SameLine();
            if (ImGuiUtils.IconButtonSized(FontAwesomeIcon.Trash, infoButtonSize))
                Service.Configuration.Macros.RemoveAt(i);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Delete macro");

            ImGui.TableNextColumn();
            var j = 0;
            foreach (var action in macro.Actions)
            {
                ImGui.Image(action.GetIcon(RecipeClassJob).ImGuiHandle, actionSize);
                if (j++ % actionCount != actionCount - 1)
                    ImGui.SameLine();
                if (j == actionCount * 2)
                    break;
            }
            ImGui.Dummy(Vector2.Zero);
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void OpenSimulatorWindow(Macro? macro)
    {
        Service.Plugin.OpenSimulatorWindow(Recipe.ItemResult.Value!, Recipe.IsExpert, CharacterSimulationInput, RecipeClassJob, macro);
    }

    private void CopyMacroToClipboard(Macro macro)
    {
        var s = new StringBuilder();
        if (ImGui.IsKeyDown(ImGuiKey.ModShift))
        {
            foreach (var action in macro.Actions)
                s.AppendLine($"/ac \"{action.GetName(RecipeClassJob)}\"");
        }
        else
        {
            foreach (var action in macro.Actions)
                s.AppendLine($"/ac \"{action.GetName(RecipeClassJob)}\" <wait.{action.Base().MacroWaitTime}>");
            s.AppendLine($"/echo Macro Complete! <se.1>");
        }
        ImGui.SetClipboardText(s.ToString());
    }

    private void DrawGearsets()
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

            var items = Gearsets.GetGearsetItems(gearset);
            var stats = Gearsets.CalculateCharacterStats(items, RecipeCharacterLevel, RecipeCanUseManipulation);
            var gearsetId = gearset->ID + 1;

            ImGuiUtils.BeginGroupPanel($"{SafeMemory.ReadString((nint)gearset->Name, 47)} ({gearsetId})");
            ImGui.Text(GetCharacterStatsText(stats));
            ImGui.SameLine();
            if (ImGuiComponents.IconButton($"SwapGearset{gearsetId}", FontAwesomeIcon.SyncAlt))
                Chat.SendMessage($"/gearset change {gearsetId}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Swap to gearset {gearsetId}");
            ImGuiUtils.EndGroupPanel();
        }
    }

    public override bool DrawConditions()
    {
        if (Service.ClientState.LocalPlayer == null)
            return false;

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

        var isNewRecipe = RecipeId != recipeEntry->RecipeId;

        RecipeId = recipeEntry->RecipeId;

        var recipe = LuminaSheets.RecipeSheet.GetRow(RecipeId);

        if (recipe == null)
            return false;

        Recipe = recipe;

        if (!Addon->Unk258->IsVisible)
            return false;

        if (isNewRecipe)
        {
            QualityNotches = 0;
            CalculateRecipeStats();
        }

        return base.DrawConditions();
    }

    public override unsafe void PreDraw()
    {
        Stopwatch.Restart();
        ref var unit = ref Addon->AtkUnitBase;
        var scale = unit.Scale;
        var pos = new Vector2(unit.X, unit.Y);
        var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

        var node = (AtkResNode*)Addon->Unk458; // unit.GetNodeById(59);
        var nodeParent = Addon->Unk258; // unit.GetNodeById(57);

        Position = pos + new Vector2(size.X, (nodeParent->Y + node->Y) * scale);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(-1),
            MaximumSize = new(10000, 10000)
        };

        CalculateCharacterStats();

        base.PreDraw();
    }

    public override void PostDraw()
    {
        Stopwatch.Stop();
        FrameTime = Stopwatch.Elapsed;
        base.PostDraw();
    }

    private Gearsets.GearsetStats CalculateConsumableBonus(CharacterStats stats)
    {
        static int CalculateBonus(int param, bool isHq, FoodStat? stat)
        {
            if (stat == null)
                return 0;

            var foodStat = stat.Value;
            var (value, max) = isHq ? (foodStat.ValueHQ, foodStat.MaxHQ) : (foodStat.Value, foodStat.Max);

            if (!foodStat.IsRelative)
                return value;

            return Math.Min((int)MathF.Floor((float)value * param), max);
        }

        Gearsets.GearsetStats ret = new();

        if (SelectedFood != null)
        {
            ret.CP += CalculateBonus(stats.CP, SelectedFoodHQ, SelectedFood.CP);
            ret.Craftsmanship += CalculateBonus(stats.Craftsmanship, SelectedFoodHQ, SelectedFood.Craftsmanship);
            ret.Control += CalculateBonus(stats.Control, SelectedFoodHQ, SelectedFood.Control);
        }

        if (SelectedMedicine != null)
        {
            ret.CP += CalculateBonus(stats.CP, SelectedMedicineHQ, SelectedMedicine.CP);
            ret.Craftsmanship += CalculateBonus(stats.Craftsmanship, SelectedMedicineHQ, SelectedMedicine.Craftsmanship);
            ret.Control += CalculateBonus(stats.Control, SelectedMedicineHQ, SelectedMedicine.Control);
        }

        return ret;
    }

    private enum CannotCraftReason
    {
        OK,
        WrongClassJob,
        SpecialistRequired,
        RequiredItem,
        RequiredStatus,
        CraftsmanshipTooLow,
        ControlTooLow,
    }

    private CannotCraftReason CanCraftRecipe(Gearsets.GearsetItem[] items, CharacterStats stats)
    {
        if (!ClassJobUtils.IsClassJob((byte)Service.ClientState.LocalPlayer!.ClassJob.Id, RecipeClassJob))
        {
            return CannotCraftReason.WrongClassJob;
        }

        if (Recipe.IsSpecializationRequired && !stats.IsSpecialist)
            return CannotCraftReason.SpecialistRequired;

        if (Recipe.ItemRequired.Row != 0)
        {
            if (Recipe.ItemRequired.Value != null)
            {
                if (!items.Any(i => Gearsets.IsItem(i, Recipe.ItemRequired.Row)))
                {
                    return CannotCraftReason.RequiredItem;
                }
            }
        }

        if (Recipe.StatusRequired.Row != 0)
        {
            if (Recipe.StatusRequired.Value != null)
            {
                if (!Service.ClientState.LocalPlayer.StatusList.Any(s => s.StatusId == Recipe.StatusRequired.Row))
                {
                    return CannotCraftReason.RequiredStatus;
                }
            }
        }

        if (Recipe.RequiredCraftsmanship > stats.Craftsmanship)
            return CannotCraftReason.CraftsmanshipTooLow;

        if (Recipe.RequiredControl > stats.Control)
            return CannotCraftReason.ControlTooLow;

        return CannotCraftReason.OK;
    }

    private static RecipeInfo CreateRecipeInfo(Recipe recipe)
    {
        var recipeTable = recipe.RecipeLevelTable.Value!;
        return new()
        {
            IsExpert = recipe.IsExpert,
            ClassJobLevel = recipeTable.ClassJobLevel,
            RLvl = (int)recipeTable.RowId,
            ConditionsFlag = recipeTable.ConditionsFlag,
            MaxDurability = recipeTable.Durability * recipe.DurabilityFactor / 100,
            MaxQuality = (int)recipeTable.Quality * recipe.QualityFactor / 100,
            MaxProgress = recipeTable.Difficulty * recipe.DifficultyFactor / 100,
            QualityModifier = recipeTable.QualityModifier,
            QualityDivider = recipeTable.QualityDivider,
            ProgressModifier = recipeTable.ProgressModifier,
            ProgressDivider = recipeTable.ProgressDivider,
        };
    }

    private static string GetCannotCraftReasonText(CannotCraftReason reason) =>
        reason switch
        {
            CannotCraftReason.OK => "You can craft this recipe.",
            CannotCraftReason.WrongClassJob => "Your current class cannot craft this recipe.",
            CannotCraftReason.SpecialistRequired => "You must be a specialist to craft this recipe.",
            CannotCraftReason.RequiredItem => "You do not have the required item to craft this recipe.",
            CannotCraftReason.RequiredStatus => "You do not have the required status effect to craft this recipe.",
            CannotCraftReason.CraftsmanshipTooLow => "Your craftsmanship is too low to craft this recipe.",
            CannotCraftReason.ControlTooLow => "Your control is too low to craft this recipe.",
            _ => "Unknown reason.",
        };

    private static string GetCharacterStatsText(CharacterStats stats)
    {
        var s = new StringBuilder();
        s.AppendLine($"Level {stats.Level} (CLvl {stats.CLvl})");
        s.AppendLine($"Craftsmanship {stats.Craftsmanship}");
        s.AppendLine($"Control {stats.Control}");
        s.AppendLine($"CP {stats.CP}");
        if (stats.IsSpecialist)
            s.AppendLine($" + Specialist");
        if (stats.HasSplendorousBuff)
            s.AppendLine($" + Splendorous Tool");
        return s.ToString();
    }
}
