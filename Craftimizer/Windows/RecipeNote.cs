using Craftimizer.Plugin;
using Craftimizer.Plugin.Utils;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;
using Craftimizer.Utils;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using ClassJob = Craftimizer.Simulator.ClassJob;
using CSRecipeNote = FFXIVClientStructs.FFXIV.Client.Game.UI.RecipeNote;

namespace Craftimizer.Windows;

public sealed unsafe class RecipeNote : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration
      | ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoSavedSettings
      | ImGuiWindowFlags.NoFocusOnAppearing
      | ImGuiWindowFlags.NoNavFocus;

    public enum CraftableStatus
    {
        OK,
        LockedClassJob,
        WrongClassJob,
        SpecialistRequired,
        RequiredItem,
        RequiredStatus,
        CraftsmanshipTooLow,
        ControlTooLow,
    }

    public AddonRecipeNote* Addon { get; private set; }
    public RecipeData? RecipeData { get; private set; }
    public CharacterStats? CharacterStats { get; private set; }
    public CraftableStatus CraftStatus { get; private set; }

    private CancellationTokenSource? BestMacroTokenSource { get; set; }
    private Exception? BestMacroException { get; set; }
    public (Macro, SimulationState)? BestSavedMacro { get; private set; }
    public bool HasSavedMacro { get; private set; }
    public SolverSolution? BestSuggestedMacro { get; private set; }

    private IDalamudTextureWrap ExpertBadge { get; }
    private IDalamudTextureWrap CollectibleBadge { get; }
    private IDalamudTextureWrap SplendorousBadge { get; }
    private IDalamudTextureWrap SpecialistBadge { get; }
    private IDalamudTextureWrap NoManipulationBadge { get; }
    private GameFontHandle AxisFont { get; }

    public RecipeNote() : base("Craftimizer RecipeNote", WindowFlags, false)
    {
        ExpertBadge = Service.IconManager.GetAssemblyTexture("Graphics.expert_badge.png");
        CollectibleBadge = Service.IconManager.GetAssemblyTexture("Graphics.collectible_badge.png");
        SplendorousBadge = Service.IconManager.GetAssemblyTexture("Graphics.splendorous.png");
        SpecialistBadge = Service.IconManager.GetAssemblyTexture("Graphics.specialist.png");
        NoManipulationBadge = Service.IconManager.GetAssemblyTexture("Graphics.no_manip.png");
        AxisFont = Service.PluginInterface.UiBuilder.GetGameFontHandle(new(GameFontFamilyAndSize.Axis14));

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        IsOpen = true;

        Service.WindowSystem.AddWindow(this);
    }

    private bool wasOpen;
    public override bool DrawConditions()
    {
        var isOpen = ShouldDraw();
        if (isOpen != wasOpen)
        {
            if (wasOpen)
                BestMacroTokenSource?.Cancel();
        }

        wasOpen = isOpen;
        return isOpen;
    }

    private bool ShouldDraw()
    {
        if (Service.ClientState.LocalPlayer == null)
            return false;

        {
            Addon = (AddonRecipeNote*)Service.GameGui.GetAddonByName("RecipeNote");
            if (Addon == null)
                return false;

            // Check if RecipeNote addon is visible
            if (Addon->AtkUnitBase.WindowNode == null)
                return false;

            // Check if RecipeNote has a visible selected recipe
            if (!Addon->Unk258->IsVisible)
                return false;
        }

        var statsChanged = false;
        {
            var instance = CSRecipeNote.Instance();

            var list = instance->RecipeList;
            if (list == null)
                return false;

            var recipeEntry = list->SelectedRecipe;
            if (recipeEntry == null)
                return false;

            var recipeId = recipeEntry->RecipeId;
            if (recipeId != RecipeData?.RecipeId)
            {
                RecipeData = new(recipeId);
                statsChanged = true;
            }
        }

        Gearsets.GearsetItem[] gearItems;
        {
            var gearStats = Gearsets.CalculateGearsetCurrentStats();

            var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            if (container == null)
                return false;

            gearItems = Gearsets.GetGearsetItems(container);

            var characterStats = Gearsets.CalculateCharacterStats(gearStats, gearItems, RecipeData.ClassJob.GetPlayerLevel(), RecipeData.ClassJob.CanPlayerUseManipulation());
            if (characterStats != CharacterStats)
            {
                CharacterStats = characterStats;
                statsChanged = true;
            }
        }

        var craftStatus = CalculateCraftStatus(gearItems);
        if (craftStatus != CraftStatus)
        {
            CraftStatus = craftStatus;
            statsChanged = true;
        }

        if ((statsChanged || (BestMacroTokenSource?.IsCancellationRequested ?? false)) && CraftStatus == CraftableStatus.OK)
            CalculateBestMacros();

        return true;
    }

    public override void PreDraw()
    {
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
    }

    public override void Draw()
    {
        var availWidth = ImGui.GetContentRegionAvail().X;
        using (var table = ImRaii.Table("stats", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedSame))
        {
            if (table)
            {
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthFixed, 0);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthFixed, 0);
                ImGui.TableNextColumn();
                DrawCharacterStats();
                ImGui.TableNextColumn();
                DrawRecipeStats();

                // Ensure that we know the window should be the same size as this table. Any more and it'll grow slowly and won't shrink when it could
                ImGui.SameLine(0, 0);
                // The -1 is to account for the extra vertical separator on the right that ImGui draws for some reason
                availWidth = ImGui.GetCursorPosX() - ImGui.GetStyle().WindowPadding.X + ImGui.GetStyle().CellPadding.X - 1;
            }
        }

        if (CraftStatus != CraftableStatus.OK)
            return;

        ImGui.Separator();

        using (var table = ImRaii.Table("macros", 1, ImGuiTableFlags.SizingStretchSame))
        {
            if (table)
            {
                ImGui.TableNextColumn();

                availWidth -= ImGui.GetStyle().ItemSpacing.X * 2;
                using (var panel = ImGuiUtils.GroupPanel("Best Saved Macro", availWidth, out _))
                {
                    var stepsAvailWidthOffset = ImGui.GetContentRegionAvail().X - availWidth;
                    if (BestSavedMacro is { } savedMacro)
                    {
                        ImGuiUtils.TextCentered(savedMacro.Item1.Name, availWidth);
                        DrawMacro((savedMacro.Item1.Actions, savedMacro.Item2), a => { savedMacro.Item1.ActionEnumerable = a; Service.Configuration.Save(); }, stepsAvailWidthOffset, true);
                    }
                    else
                    {
                        ImGui.Text("");
                        DrawMacro(null, null, stepsAvailWidthOffset, true);
                    }
                }

                using (var panel = ImGuiUtils.GroupPanel("Suggested Macro", availWidth, out _))
                {
                    var stepsAvailWidthOffset = ImGui.GetContentRegionAvail().X - availWidth;
                    if (BestSuggestedMacro is { } suggestedMacro)
                        DrawMacro((suggestedMacro.Actions, suggestedMacro.State), null, stepsAvailWidthOffset, false);
                    else
                        DrawMacro(null, null, stepsAvailWidthOffset, false);
                }

                ImGuiHelpers.ScaledDummy(5);

                if (ImGui.Button("View Saved Macros", new(-1, 0)))
                    Service.Plugin.OpenMacroListWindow();

                if (ImGui.Button("Open in Simulator", new(-1, 0)))
                    Service.Plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.ClientState.LocalPlayer!.StatusList), Enumerable.Empty<ActionType>(), null);
            }
        }
    }

    private void DrawCharacterStats()
    {
        ImGuiUtils.TextCentered("Crafter");

        var level = RecipeData!.ClassJob.GetPlayerLevel();
        {
            var textClassName = RecipeData.ClassJob.GetAbbreviation();
            Vector2 textClassSize;
            {
                var layout = AxisFont.LayoutBuilder(textClassName).Build();
                textClassSize = new(layout.Width, layout.Height);
            }
            var levelText = string.Empty;
            if (level != 0)
                levelText = SqText.LevelPrefix.ToIconChar() + SqText.ToLevelString(level);
            var imageSize = ImGui.GetFrameHeight();
            bool hasSplendorous = false, hasSpecialist = false, shouldHaveManip = false;
            if (CraftStatus is not (CraftableStatus.LockedClassJob or CraftableStatus.WrongClassJob))
            {
                hasSplendorous = CharacterStats!.HasSplendorousBuff;
                hasSpecialist = CharacterStats!.IsSpecialist;
                shouldHaveManip = !CharacterStats.CanUseManipulation && CharacterStats.Level >= ActionType.Manipulation.Level();
            }

            ImGuiUtils.AlignCentered(
                imageSize + 5 +
                textClassSize.X +
                (level == 0 ? 0 : (3 + ImGui.CalcTextSize(levelText).X)) +
                (hasSplendorous ? (3 + imageSize) : 0) +
                (hasSpecialist ? (3 + imageSize) : 0) +
                (shouldHaveManip ? (3 + imageSize) : 0)
                );
            ImGui.AlignTextToFramePadding();

            var uv0 = new Vector2(6, 3);
            var uv1 = uv0 + new Vector2(44);
            uv0 /= new Vector2(56);
            uv1 /= new Vector2(56);

            ImGui.Image(Service.IconManager.GetIcon(RecipeData.ClassJob.GetIconId()).ImGuiHandle, new Vector2(imageSize), uv0, uv1);
            ImGui.SameLine(0, 5);

            if (level != 0)
            {
                ImGui.Text(levelText);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"CLvl {Gearsets.CalculateCLvl(level)}");
                ImGui.SameLine(0, 3);
            }

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (imageSize - textClassSize.Y) / 2);
            AxisFont.Text(textClassName);

            if (hasSplendorous)
            {
                ImGui.SameLine(0, 3);
                ImGui.Image(SplendorousBadge.ImGuiHandle, new Vector2(imageSize));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Splendorous Tool");
            }

            if (hasSpecialist)
            {
                ImGui.SameLine(0, 3);
                ImGui.Image(SpecialistBadge.ImGuiHandle, new Vector2(imageSize), Vector2.Zero, Vector2.One, new(0.99f, 0.97f, 0.62f, 1f));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Specialist");
            }

            if (shouldHaveManip)
            {
                ImGui.SameLine(0, 3);
                ImGui.Image(NoManipulationBadge.ImGuiHandle, new Vector2(imageSize));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"No Manipulation (Missing Job Quest)");
            }
        }

        ImGui.Separator();

        switch (CraftStatus)
        {
            case CraftableStatus.LockedClassJob:
                {
                    ImGuiUtils.TextCentered($"You do not have {RecipeData.ClassJob.GetName().ToLowerInvariant()} unlocked.");
                    ImGui.Separator();
                    var unlockQuest = RecipeData.ClassJob.GetUnlockQuest();
                    var (questGiver, questTerritory, questLocation, mapPayload) = ResolveLevelData(unlockQuest.IssuerLocation.Row);

                    var unlockText = $"Unlock it from {questGiver}";
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(unlockText).X + 5 + ImGui.GetFrameHeight());
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(unlockText);
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Flag))
                        Service.GameGui.OpenMapWithMapLink(mapPayload);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Open in map");

                    ImGuiUtils.TextCentered($"{questTerritory} ({questLocation.X:0.0}, {questLocation.Y:0.0})");
                }
                break;
            case CraftableStatus.WrongClassJob:
                {
                    ImGuiUtils.TextCentered($"You are not a {RecipeData.ClassJob.GetName().ToLowerInvariant()}.");
                    var gearsetId = GetGearsetForJob(RecipeData.ClassJob);
                    if (gearsetId.HasValue)
                    {
                        if (ImGuiUtils.ButtonCentered("Switch Job"))
                            Chat.SendMessage($"/gearset change {gearsetId + 1}");
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"Swap to gearset {gearsetId + 1}");
                    }
                    else
                        ImGuiUtils.TextCentered($"You do not have any {RecipeData.ClassJob.GetName().ToLowerInvariant()} gearsets.");
                    ImGui.Dummy(default);
                }
                break;
            case CraftableStatus.SpecialistRequired:
                {
                    ImGuiUtils.TextCentered($"You need to be a specialist to craft this recipe.");

                    var (vendorName, vendorTerritory, vendorLoation, mapPayload) = ResolveLevelData(5891399);

                    var unlockText = $"Trade a Soul of the Crafter to {vendorName}";
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(unlockText).X + 5 + ImGui.GetFrameHeight());
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(unlockText);
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Flag))
                        Service.GameGui.OpenMapWithMapLink(mapPayload);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Open in map");

                    ImGuiUtils.TextCentered($"{vendorTerritory} ({vendorLoation.X:0.0}, {vendorLoation.Y:0.0})");
                }
                break;
            case CraftableStatus.RequiredItem:
                {
                    var item = RecipeData.Recipe.ItemRequired.Value!;
                    var itemName = item.Name.ToDalamudString().ToString();
                    var imageSize = ImGui.GetFrameHeight();

                    ImGuiUtils.TextCentered($"You are missing the required equipment.");
                    ImGuiUtils.AlignCentered(imageSize + 5 + ImGui.CalcTextSize(itemName).X);
                    ImGui.AlignTextToFramePadding();
                    ImGui.Image(Service.IconManager.GetIcon(item.Icon).ImGuiHandle, new(imageSize));
                    ImGui.SameLine(0, 5);
                    ImGui.Text(itemName);
                }
                break;
            case CraftableStatus.RequiredStatus:
                {
                    var status = RecipeData.Recipe.StatusRequired.Value!;
                    var statusName = status.Name.ToDalamudString().ToString();
                    var statusIcon = Service.IconManager.GetIcon(status.Icon);
                    var imageSize = new Vector2(ImGui.GetFrameHeight() * statusIcon.Width / statusIcon.Height, ImGui.GetFrameHeight());

                    ImGuiUtils.TextCentered($"You are missing the required status effect.");
                    ImGuiUtils.AlignCentered(imageSize.X + 5 + ImGui.CalcTextSize(statusName).X);
                    ImGui.AlignTextToFramePadding();
                    ImGui.Image(statusIcon.ImGuiHandle, imageSize);
                    ImGui.SameLine(0, 5);
                    ImGui.Text(statusName);
                }
                break;
            case CraftableStatus.CraftsmanshipTooLow:
                {
                    ImGuiUtils.TextCentered("Your Craftsmanship is too low.");

                    DrawRequiredStatsTable(CharacterStats!.Craftsmanship, RecipeData.Recipe.RequiredCraftsmanship);
                }
                break;
            case CraftableStatus.ControlTooLow:
                {
                    ImGuiUtils.TextCentered("Your Control is too low.");

                    DrawRequiredStatsTable(CharacterStats!.Control, RecipeData.Recipe.RequiredControl);
                }
                break;
            case CraftableStatus.OK:
                {
                    using var table = ImRaii.Table("characterStats", 2, ImGuiTableFlags.NoHostExtendX);
                    if (table)
                    {
                        ImGui.TableSetupColumn("ccol1", ImGuiTableColumnFlags.WidthFixed, 100);
                        ImGui.TableSetupColumn("ccol2", ImGuiTableColumnFlags.WidthStretch);

                        ImGui.TableNextColumn();
                        ImGui.Text("Craftsmanship");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{CharacterStats!.Craftsmanship}");

                        ImGui.TableNextColumn();
                        ImGui.Text("Control");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{CharacterStats.Control}");

                        ImGui.TableNextColumn();
                        ImGui.Text("CP");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{CharacterStats.CP}");
                    }
                }
                break;
        }
    }

    private void DrawRecipeStats()
    {
        ImGuiUtils.TextCentered("Recipe");

        {
            var textStars = new string('★', RecipeData!.Table.Stars);
            var textStarsSize = Vector2.Zero;
            if (!string.IsNullOrEmpty(textStars)) {
                var layout = AxisFont.LayoutBuilder(textStars).Build();
                textStarsSize = new(layout.Width, layout.Height);
            }
            var textLevel = SqText.LevelPrefix.ToIconChar() + SqText.ToLevelString(RecipeData.RecipeInfo.ClassJobLevel);
            var isExpert = RecipeData.RecipeInfo.IsExpert;
            var isCollectable = RecipeData.Recipe.ItemResult.Value!.IsCollectable;
            var imageSize = ImGui.GetFrameHeight();
            var textSize = ImGui.GetFontSize();
            var badgeSize = new Vector2(textSize * ExpertBadge.Width / ExpertBadge.Height, textSize);
            var badgeOffset = (imageSize - badgeSize.Y) / 2;

            ImGuiUtils.AlignCentered(
                imageSize + 5 +
                ImGui.CalcTextSize(textLevel).X +
                (textStarsSize != Vector2.Zero ? textStarsSize.X + 3 : 0) +
                (isCollectable ? badgeSize.X + 3 : 0) +
                (isExpert ? badgeSize.X + 3 : 0)
                );
            ImGui.AlignTextToFramePadding();

            ImGui.Image(Service.IconManager.GetIcon(RecipeData.Recipe.ItemResult.Value!.Icon).ImGuiHandle, new Vector2(imageSize));

            ImGui.SameLine(0, 5);
            ImGui.Text(textLevel);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"RLvl {RecipeData.RecipeInfo.RLvl}");

            if (textStarsSize != Vector2.Zero)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (imageSize - textStarsSize.Y) / 2);
                AxisFont.Text(textStars);
            }

            if (isCollectable)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + badgeOffset);
                ImGui.Image(CollectibleBadge.ImGuiHandle, badgeSize);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Collectible");
            }

            if (isExpert)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + badgeOffset);
                ImGui.Image(ExpertBadge.ImGuiHandle, badgeSize);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Expert Recipe");
            }
        }

        ImGui.Separator();

        using var table = ImRaii.Table("recipeStats", 2);
        if (table)
        {
            ImGui.TableSetupColumn("rcol1", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("rcol2", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.Text("Progress");
            ImGui.TableNextColumn();
            ImGui.Text($"{RecipeData.RecipeInfo.MaxProgress}");

            ImGui.TableNextColumn();
            ImGui.Text("Quality");
            ImGui.TableNextColumn();
            ImGui.Text($"{RecipeData.RecipeInfo.MaxQuality}");

            ImGui.TableNextColumn();
            ImGui.Text("Durability");
            ImGui.TableNextColumn();
            ImGui.Text($"{RecipeData.RecipeInfo.MaxDurability}");
        }
    }

    private void DrawMacro((IReadOnlyList<ActionType> Actions, SimulationState State)? macroValue, Action<IEnumerable<ActionType>>? setter, float stepsAvailWidthOffset, bool isSavedMacro)
    {
        var windowHeight = 2 * ImGui.GetFrameHeightWithSpacing();

        if (macroValue is not { } macro)
        {
            if (isSavedMacro && !HasSavedMacro)
                ImGuiUtils.TextMiddleNewLine("You have no macros!", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
            else if (BestMacroException == null)
                ImGuiUtils.TextMiddleNewLine("Calculating...", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
            else
            {
                ImGui.AlignTextToFramePadding();
                using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                    ImGuiUtils.TextCentered("An exception occurred");
                if (ImGuiUtils.ButtonCentered("Copy Error Message"))
                    ImGui.SetClipboardText(BestMacroException.ToString());
            }
            return;
        }
        if (macro.Actions.Any(a => a.Category() == ActionCategory.Combo))
            throw new InvalidOperationException("Combo actions should be sanitized away");

        using var table = ImRaii.Table("table", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame);
        if (table)
        {
            ImGui.TableSetupColumn("desc", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("actions", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("steps", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow(ImGuiTableRowFlags.None, windowHeight);
            ImGui.TableNextColumn();

            var spacing = ImGui.GetStyle().ItemSpacing.Y;
            var miniRowHeight = (windowHeight - spacing) / 2f;

            {
                if (Service.Configuration.ShowOptimalMacroStat)
                {
                    var progressHeight = windowHeight;
                    if (macro.State.Progress >= macro.State.Input.Recipe.MaxProgress && macro.State.Input.Recipe.MaxQuality > 0)
                    {
                        ImGuiUtils.ArcProgress(
                        (float)macro.State.Quality / macro.State.Input.Recipe.MaxQuality,
                        progressHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Quality));
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"Quality: {macro.State.Quality} / {macro.State.Input.Recipe.MaxQuality}");
                    }
                    else
                    {
                        ImGuiUtils.ArcProgress(
                        (float)macro.State.Progress / macro.State.Input.Recipe.MaxProgress,
                        progressHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Progress));
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"Progress: {macro.State.Progress} / {macro.State.Input.Recipe.MaxProgress}");
                    }
                }
                else
                {
                    ImGuiUtils.ArcProgress(
                        (float)macro.State.Progress / macro.State.Input.Recipe.MaxProgress,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Progress));
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"Progress: {macro.State.Progress} / {macro.State.Input.Recipe.MaxProgress}");

                    ImGui.SameLine(0, spacing);
                    ImGuiUtils.ArcProgress(
                        (float)macro.State.Quality / macro.State.Input.Recipe.MaxQuality,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Quality));
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"Quality: {macro.State.Quality} / {macro.State.Input.Recipe.MaxQuality}");

                    ImGuiUtils.ArcProgress((float)macro.State.Durability / macro.State.Input.Recipe.MaxDurability,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Durability));
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"Remaining Durability: {macro.State.Durability} / {macro.State.Input.Recipe.MaxDurability}");

                    ImGui.SameLine(0, spacing);
                    ImGuiUtils.ArcProgress(
                        (float)macro.State.CP / macro.State.Input.Stats.CP,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.CP));
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"Remaining CP: {macro.State.CP} / {macro.State.Input.Stats.CP}");
                }
            }
            
            ImGui.TableNextColumn();
            {
                if (ImGuiUtils.IconButtonSized(FontAwesomeIcon.Edit, new(miniRowHeight)))
                    Service.Plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.ClientState.LocalPlayer!.StatusList), macro.Actions, setter);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Open in Simulator");
                if (ImGuiUtils.IconButtonSized(FontAwesomeIcon.Copy, new(miniRowHeight)))
                    Service.Plugin.CopyMacro(macro.Actions);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Copy to Clipboard");
            }

            ImGui.TableNextColumn();
            {
                var itemsPerRow = (int)MathF.Floor((ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset + spacing) / (miniRowHeight + spacing));
                var itemCount = macro.Actions.Count;
                for (var i = 0; i < itemsPerRow * 2; i++)
                {
                    if (i % itemsPerRow != 0)
                        ImGui.SameLine(0, spacing);
                    if (i < itemCount)
                    {
                        var shouldShowMore = i + 1 == itemsPerRow * 2 && i + 1 < itemCount;
                        if (!shouldShowMore)
                        {
                            ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).ImGuiHandle, new(miniRowHeight));
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip(macro.Actions[i].GetName(RecipeData!.ClassJob));
                        }
                        else
                        {
                            var amtMore = itemCount - itemsPerRow * 2;
                            var pos = ImGui.GetCursorPos();
                            ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).ImGuiHandle, new(miniRowHeight), default, Vector2.One, new(1, 1, 1, .5f));
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip($"{macro.Actions[i].GetName(RecipeData!.ClassJob)}\nand {amtMore} more");
                            ImGui.SetCursorPos(pos);
                            ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowHeight), ImGui.GetColorU32(ImGuiCol.FrameBg), miniRowHeight / 8f);
                            ImGui.GetWindowDrawList().AddTextClippedEx(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowHeight), $"+{amtMore}", null, new(.5f), null);
                        }
                    }
                    else
                        ImGui.Dummy(new(miniRowHeight));
                }
            }
        }
    }

    private static void DrawRequiredStatsTable(int current, int required)
    {
        if (current >= required)
            throw new ArgumentOutOfRangeException(nameof(current));

        using var table = ImRaii.Table("requiredStats", 2, ImGuiTableFlags.NoHostExtendX);
        if (table)
        {
            ImGui.TableSetupColumn("ccol1", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("ccol2", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.Text("Current");
            ImGui.TableNextColumn();
            ImGui.TextColored(new(0, 1, 0, 1), $"{current}");

            ImGui.TableNextColumn();
            ImGui.Text("Required");
            ImGui.TableNextColumn();
            ImGui.TextColored(new(1, 0, 0, 1), $"{required}");

            ImGui.TableNextColumn();
            ImGui.Text("You need");
            ImGui.TableNextColumn();
            ImGui.Text($"{required - current}");
        }
    }

    private CraftableStatus CalculateCraftStatus(Gearsets.GearsetItem[] gearItems)
    {
        if (RecipeData!.ClassJob.GetPlayerLevel() == 0)
            return CraftableStatus.LockedClassJob;

        if (PlayerState.Instance()->CurrentClassJobId != RecipeData.ClassJob.GetClassJobIndex())
            return CraftableStatus.WrongClassJob;

        if (RecipeData.Recipe.IsSpecializationRequired && !(CharacterStats!.IsSpecialist))
            return CraftableStatus.SpecialistRequired;

        var itemRequired = RecipeData.Recipe.ItemRequired;
        if (itemRequired.Row != 0 && itemRequired.Value != null)
        {
            if (!gearItems.Any(i => Gearsets.IsItem(i, itemRequired.Row)))
                return CraftableStatus.RequiredItem;
        }

        var statusRequired = RecipeData.Recipe.StatusRequired;
        if (statusRequired.Row != 0 && statusRequired.Value != null)
        {
            if (!Service.ClientState.LocalPlayer!.StatusList.Any(s => s.StatusId == statusRequired.Row))
                return CraftableStatus.RequiredStatus;
        }

        if (RecipeData.Recipe.RequiredCraftsmanship > CharacterStats!.Craftsmanship)
            return CraftableStatus.CraftsmanshipTooLow;

        if (RecipeData.Recipe.RequiredControl > CharacterStats.Control)
            return CraftableStatus.ControlTooLow;

        return CraftableStatus.OK;
    }

    private static (string NpcName, string Territory, Vector2 MapLocation, MapLinkPayload Payload) ResolveLevelData(uint levelRowId)
    {
        var level = LuminaSheets.LevelSheet.GetRow(levelRowId) ??
            throw new ArgumentNullException(nameof(levelRowId), $"Invalid level row {levelRowId}");
        var territory = level.Territory.Value!.PlaceName.Value!.Name.ToDalamudString().ToString();
        var location = MapUtil.WorldToMap(new(level.X, level.Z), level.Map.Value!);

        return (ResolveNpcResidentName(level.Object), territory, location, new(level.Territory.Row, level.Map.Row, location.X, location.Y));
    }

    private static string ResolveNpcResidentName(uint npcRowId)
    {
        var resident = LuminaSheets.ENpcResidentSheet.GetRow(npcRowId) ??
            throw new ArgumentNullException(nameof(npcRowId), $"Invalid npc row {npcRowId}");
        return resident.Singular.ToDalamudString().ToString();
    }

    private static int? GetGearsetForJob(ClassJob job)
    {
        var gearsetModule = RaptureGearsetModule.Instance();
        for (var i = 0; i < 100; i++)
        {
            var gearset = gearsetModule->EntriesSpan[i];
            if (!gearset.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;
            if (gearset.ID != i)
                continue;
            if (gearset.ClassJob != job.GetClassJobIndex())
                continue;
            return i;
        }
        return null;
    }

    private void CalculateBestMacros()
    {
        BestMacroTokenSource?.Cancel();
        BestMacroTokenSource = new();
        BestMacroException = null;
        BestSavedMacro = null;
        HasSavedMacro = false;
        BestSuggestedMacro = null;

        var token = BestMacroTokenSource.Token;
        var task = Task.Run(() => CalculateBestMacrosTask(token), token);
        _ = task.ContinueWith(t =>
        {
            if (token == BestMacroTokenSource.Token)
                BestMacroTokenSource = null;
        });
        _ = task.ContinueWith(t =>
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                t.Exception!.Flatten().Handle(ex => ex is TaskCanceledException or OperationCanceledException);
            }
            catch (AggregateException e)
            {
                BestMacroException = e;
                Log.Error(e, "Calculating macros failed");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private void CalculateBestMacrosTask(CancellationToken token)
    {
        var input = new SimulationInput(CharacterStats!, RecipeData!.RecipeInfo);
        var state = new SimulationState(input);
        var config = Service.Configuration.SimulatorSolverConfig;
        var mctsConfig = new MCTSConfig(config);
        var simulator = new Solver.Simulator(state, mctsConfig.MaxStepCount);
        List<Macro> macros = new(Service.Configuration.Macros);

        token.ThrowIfCancellationRequested();

        HasSavedMacro = macros.Count > 0;
        if (HasSavedMacro)
        {
            var bestSaved = macros
                .Select(macro =>
                    {
                        var (resp, outState, failedIdx) = simulator.ExecuteMultiple(state, macro.Actions);
                        outState.ActionCount = macro.Actions.Count;
                        var score = SimulationNode.CalculateScoreForState(outState, simulator.CompletionState, mctsConfig) ?? 0;
                        if (resp != ActionResponse.SimulationComplete)
                        {
                            if (failedIdx != -1)
                                score /= 2;
                        }
                        return (macro, outState, score);
                    })
                .MaxBy(m => m.score);

            token.ThrowIfCancellationRequested();

            BestSavedMacro = (bestSaved.macro, bestSaved.outState);

            token.ThrowIfCancellationRequested();
        }

        var solver = new Solver.Solver(config, state) { Token = token };
        solver.OnLog += Log.Debug;
        solver.Start();
        var solution = solver.GetTask().GetAwaiter().GetResult();

        token.ThrowIfCancellationRequested();

        BestSuggestedMacro = solution;

        token.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        Service.WindowSystem.RemoveWindow(this);
        AxisFont?.Dispose();
    }
}
