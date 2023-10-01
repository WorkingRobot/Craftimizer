using Craftimizer.Plugin;
using Craftimizer.Plugin.Utils;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using ImGuiScene;
using System;
using System.Linq;
using System.Numerics;
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

    private TextureWrap ExpertBadge { get; }
    private TextureWrap CollectibleBadge { get; }
    private TextureWrap SplendorousBadge { get; }
    private TextureWrap SpecialistBadge { get; }
    private TextureWrap NoManipulationBadge { get; }
    private GameFontHandle AxisFont { get; }

    public RecipeNote() : base("Craftimizer RecipeNode", WindowFlags, false)
    {
        ExpertBadge = Service.IconManager.GetAssemblyTexture("Graphics.expert_badge.png");
        CollectibleBadge = Service.IconManager.GetAssemblyTexture("Graphics.collectible_badge.png");
        SplendorousBadge = Service.IconManager.GetAssemblyTexture("Graphics.splendorous.png");
        SpecialistBadge = Service.IconManager.GetAssemblyTexture("Graphics.specialist.png");
        NoManipulationBadge = Service.IconManager.GetAssemblyTexture("Graphics.no_manip.png");
        AxisFont = Service.PluginInterface.UiBuilder.GetGameFontHandle(new(GameFontFamilyAndSize.Axis14));

        Service.WindowSystem.AddWindow(this);

        IsOpen = true;
    }

    public override bool DrawConditions()
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

        {
            var instance = CSRecipeNote.Instance();

            var list = instance->RecipeList;
            if (list == null)
                return false;

            var recipeEntry = list->SelectedRecipe;
            if (recipeEntry == null)
                return false;

            var recipeId = recipeEntry->RecipeId;
            RecipeData = new(recipeId);
        }

        Gearsets.GearsetItem[] gearItems;
        {
            var gearStats = Gearsets.CalculateGearsetCurrentStats();

            var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            if (container == null)
                return false;

            gearItems = Gearsets.GetGearsetItems(container);

            CharacterStats = Gearsets.CalculateCharacterStats(gearStats, gearItems, RecipeData.ClassJob.GetPlayerLevel(), RecipeData.ClassJob.CanPlayerUseManipulation());
        }

        CraftStatus = CalculateCraftStatus(gearItems);

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
        using var table = ImRaii.Table("stats", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame);
        if (table)
        {
            ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn();
            DrawCharacterStats();
            ImGui.TableNextColumn();
            DrawRecipeStats();
        }
    }

    private void DrawCharacterStats()
    {
        ImGuiUtils.TextCentered("Crafter");

        var level = RecipeData!.ClassJob.GetPlayerLevel();
        {
            var className = RecipeData.ClassJob.GetName();
            var levelText = string.Empty;
            if (level != 0)
                levelText = SqText.ToLevelString(level);
            var imageSize = ImGuiUtils.ButtonHeight;
            bool hasSplendorous = false, hasSpecialist = false, shouldHaveManip = false;
            if (CraftStatus is not (CraftableStatus.LockedClassJob or CraftableStatus.WrongClassJob))
            {
                hasSplendorous = CharacterStats!.HasSplendorousBuff;
                hasSpecialist = CharacterStats!.IsSpecialist;
                shouldHaveManip = !CharacterStats.CanUseManipulation && CharacterStats.Level >= ActionType.Manipulation.Level();
            }

            ImGuiUtils.AlignCentered(
                imageSize + 5 +
                ImGui.CalcTextSize(className).X +
                (level == 0 ? 0 : (3 + ImGui.CalcTextSize(levelText).X)) +
                (hasSplendorous ? (3 + imageSize) : 0) +
                (hasSpecialist ? (3 + imageSize) : 0) +
                (shouldHaveManip ? (3 + imageSize) : 0)
                );
            ImGui.AlignTextToFramePadding();

            ImGui.Image(Service.IconManager.GetIcon(RecipeData.ClassJob.GetIconId()).ImGuiHandle, new Vector2(imageSize));
            ImGui.SameLine(0, 5);

            if (level != 0)
            {
                ImGui.Text(levelText);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"CLvl {Gearsets.CalculateCLvl(level)}");
                ImGui.SameLine(0, 3);
            }

            ImGui.Text(className);

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
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(unlockText).X + 5 + ImGuiUtils.ButtonHeight);
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
                }
                break;
            case CraftableStatus.SpecialistRequired:
                {
                    ImGuiUtils.TextCentered($"You need to be a specialist to craft this recipe.");

                    var (vendorName, vendorTerritory, vendorLoation, mapPayload) = ResolveLevelData(5891399);

                    var unlockText = $"Trade a Soul of the Crafter to {vendorName}";
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(unlockText).X + 5 + ImGuiUtils.ButtonHeight);
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
                    var imageSize = ImGuiUtils.ButtonHeight;

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
                    var imageSize = new Vector2(ImGuiUtils.ButtonHeight * statusIcon.Width / statusIcon.Height, ImGuiUtils.ButtonHeight);

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
                textStarsSize = new(layout.Width + 3, layout.Height);
            }
            var textLevel = SqText.ToLevelString(RecipeData.RecipeInfo.ClassJobLevel);
            var isExpert = RecipeData.RecipeInfo.IsExpert;
            var isCollectable = RecipeData.Recipe.ItemResult.Value!.IsCollectable;
            var imageSize = ImGuiUtils.ButtonHeight;
            var textSize = ImGui.CalcTextSize("A").Y;
            var badgeSize = new Vector2(textSize * ExpertBadge.Width / ExpertBadge.Height, textSize);
            var badgeOffset = (imageSize - badgeSize.Y) / 2;

            ImGuiUtils.AlignCentered(
                imageSize + 5 +
                ImGui.CalcTextSize(textLevel).X +
                textStarsSize.X +
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
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (textStarsSize.Y - textSize) / 2);
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
            var gearset = gearsetModule->Gearset[i];
            if (gearset == null)
                continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;
            if (gearset->ID != i)
                continue;
            if (gearset->ClassJob != job.GetClassJobIndex())
                continue;
            return i;
        }
        return null;
    }

    public void Dispose()
    {
        AxisFont?.Dispose();
    }
}
