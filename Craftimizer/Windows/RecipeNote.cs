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
using Dalamud.Interface.ManagedFontAtlas;
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
    private const ImGuiWindowFlags WindowFlagsPinned = WindowFlagsFloating
      | ImGuiWindowFlags.NoSavedSettings;

    private const ImGuiWindowFlags WindowFlagsFloating =
        ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoFocusOnAppearing;

    private const string WindowNamePinned = "Craftimizer Crafting Log Helper###CraftimizerRecipeNote";
    private const string WindowNameFloating = $"{WindowNamePinned}Floating";

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

    public sealed class BackgroundTask<T>(Func<CancellationToken, T> func) : IDisposable where T : struct
    {
        public T? Result { get; private set; }
        public Exception? Exception { get; private set; }
        public bool Completed { get; private set; }

        private CancellationTokenSource TokenSource { get; } = new();
        private Func<CancellationToken, T> Func { get; } = func;

        public void Start()
        {
            var token = TokenSource.Token;
            var task = Task.Run(() => Result = Func(token), token);
            _ = task.ContinueWith(t => Completed = true);
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
                    Exception = e;
                    Log.Error(e, "Calculating macros failed");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Cancel() =>
            TokenSource.Cancel();

        public void Dispose() =>
            Cancel();
    }

    private BackgroundTask<(Macro?, SimulationState?)>? SavedMacroTask { get; set; }
    private BackgroundTask<SolverSolution>? SuggestedMacroTask { get; set; }
    private BackgroundTask<(CommunityMacros.CommunityMacro?, SimulationState?)>? CommunityMacroTask { get; set; }

    private Solver.Solver? BestMacroSolver { get; set; }
    public bool HasSavedMacro { get; private set; }

    private IDalamudTextureWrap ExpertBadge { get; }
    private IDalamudTextureWrap CollectibleBadge { get; }
    private IDalamudTextureWrap SplendorousBadge { get; }
    private IDalamudTextureWrap SpecialistBadge { get; }
    private IDalamudTextureWrap NoManipulationBadge { get; }
    private IFontHandle AxisFont { get; }

    public RecipeNote() : base(WindowNamePinned)
    {
        ExpertBadge = Service.IconManager.GetAssemblyTexture("Graphics.expert_badge.png");
        CollectibleBadge = Service.IconManager.GetAssemblyTexture("Graphics.collectible_badge.png");
        SplendorousBadge = Service.IconManager.GetAssemblyTexture("Graphics.splendorous.png");
        SpecialistBadge = Service.IconManager.GetAssemblyTexture("Graphics.specialist.png");
        NoManipulationBadge = Service.IconManager.GetAssemblyTexture("Graphics.no_manip.png");
        AxisFont = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis14));

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        IsOpen = true;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(-1),
            MaximumSize = new(10000, 10000)
        };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2.5f, 1),
                Click = _ => Service.Plugin.OpenSettingsWindow(),
                ShowTooltip = () => ImGuiUtils.Tooltip("Open Craftimizer Settings")
            }
        ];

        Service.WindowSystem.AddWindow(this);
    }

    private bool IsCollapsed { get; set; }
    private bool ShouldOpen { get; set; }

    private bool WasOpen { get; set; }
    private bool WasCollapsed { get; set; }

    private bool ShouldCalculate => !IsCollapsed && ShouldOpen;
    private bool WasCalculatable { get; set; }

    public override void Update()
    {
        base.Update();

        ShouldOpen = CalculateShouldOpen();

        if (ShouldCalculate != WasCalculatable)
        {
            if (WasCalculatable)
            {
                SavedMacroTask?.Cancel();
                SuggestedMacroTask?.Cancel();
                CommunityMacroTask?.Cancel();
            }
            else if (CraftStatus == CraftableStatus.OK && !StatsChanged)
            {
                // If it didn't exist before or it already ran, we need to recalculate
                if (SavedMacroTask?.Result == null && (SavedMacroTask?.Completed ?? true))
                    CalculateSavedMacro();

                // If it didn't exist before or it already ran, we need to recalculate
                if (Service.Configuration.SuggestMacroAutomatically && SuggestedMacroTask?.Result == null && (SuggestedMacroTask?.Completed ?? true))
                    CalculateSuggestedMacro();
                // If we don't want to suggest automatically, we should cancel and clean out the task
                else if (!Service.Configuration.SuggestMacroAutomatically && SuggestedMacroTask?.Result == null)
                {
                    SuggestedMacroTask?.Cancel();
                    SuggestedMacroTask = null;
                }

                // If it didn't exist before or it already ran, we need to recalculate
                if (Service.Configuration.ShowCommunityMacros && Service.Configuration.SearchCommunityMacroAutomatically && CommunityMacroTask?.Result == null && (CommunityMacroTask?.Completed ?? true))
                    CalculateCommunityMacro();
                // If we don't want to search automatically, we should cancel and clean out the task
                else if (!Service.Configuration.SearchCommunityMacroAutomatically && CommunityMacroTask?.Result == null)
                {
                    CommunityMacroTask?.Cancel();
                    CommunityMacroTask = null;
                }
            }
        }

        if (!ShouldOpen)
        {
            StyleAlpha = LastAlpha = null;
            LastPosition = null;
        }

        WasOpen = ShouldOpen;
        WasCollapsed = IsCollapsed;
        WasCalculatable = ShouldCalculate;
    }

    public override bool DrawConditions() =>
        ShouldOpen;

    private bool StatsChanged { get; set; }
    private bool CalculateShouldOpen()
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

        StatsChanged = false;
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
                StatsChanged = true;
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
                StatsChanged = true;
            }
        }

        var craftStatus = CalculateCraftStatus(gearItems);
        if (craftStatus != CraftStatus)
        {
            CraftStatus = craftStatus;
            StatsChanged = true;
        }

        if (StatsChanged && CraftStatus == CraftableStatus.OK)
        {
            // Stats changed and we are still craftable, so we need to recalculate
            CalculateSavedMacro();

            // If we want to suggest automatically, we should recalculate
            if (Service.Configuration.SuggestMacroAutomatically)
                CalculateSuggestedMacro();
            // Otherwise, we should cancel and clean out the task
            else
            {
                SuggestedMacroTask?.Cancel();
                SuggestedMacroTask = null;
            }
            
            // If we want to search automatically, we should recalculate
            if (Service.Configuration.ShowCommunityMacros && Service.Configuration.SearchCommunityMacroAutomatically)
                CalculateCommunityMacro();
            // Otherwise, we should cancel and clean out the task
            else
            {
                CommunityMacroTask?.Cancel();
                CommunityMacroTask = null;
            }
        }

        return true;
    }

    private Vector2? LastPosition { get; set; }
    private byte? StyleAlpha { get; set; }
    private byte? LastAlpha { get; set; }
    public override void PreDraw()
    {
        base.PreDraw();

        IsCollapsed = true;

        if (Service.Configuration.PinRecipeNoteToWindow)
        {
            ref var unit = ref Addon->AtkUnitBase;
            var scale = unit.Scale;
            var pos = new Vector2(unit.X, unit.Y);
            var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

            var node = (AtkResNode*)Addon->Unk458; // unit.GetNodeById(59);
            var nodeParent = Addon->Unk258; // unit.GetNodeById(57);

            var newAlpha = unit.WindowNode->AtkResNode.Alpha_2;
            StyleAlpha = LastAlpha ?? newAlpha;
            LastAlpha = newAlpha;

            var newPosition = pos + new Vector2(size.X, (nodeParent->Y + node->Y) * scale);
            Position = ImGuiHelpers.MainViewport.Pos + (LastPosition ?? newPosition);
            LastPosition = newPosition;
            Flags = WindowFlagsPinned;
            WindowName = WindowNamePinned;
        }
        else
        {
            StyleAlpha = LastAlpha = null;
            Position = LastPosition = null;
            Flags = WindowFlagsFloating;
            WindowName = WindowNameFloating;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, StyleAlpha.HasValue ? (StyleAlpha.Value / 255f) : 1);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();

        base.PostDraw();
    }

    public override void Draw()
    {
        IsCollapsed = false;

        var availWidth = ImGui.GetContentRegionAvail().X;
        using (var table = ImRaii.Table("stats", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedSame | ImGuiTableFlags.NoSavedSettings))
        {
            if (table)
            {
                if (StatsChanged)
                {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 150);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 150);
                }

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

        var panelWidth = availWidth - ImGui.GetStyle().ItemSpacing.X * 2;

        {
            var macroTaskResult = SavedMacroTask?.Result;
            var state = new MacroTaskState()
            {
                Type = MacroTaskType.Saved,
                Exception = SavedMacroTask?.Exception,
                Started = SavedMacroTask != null,
                Completed = SavedMacroTask?.Completed ?? false,
                Actions = macroTaskResult?.Item1?.Actions,
                MacroName = macroTaskResult?.Item1?.Name,
                State = macroTaskResult?.Item2,
            };
            if (macroTaskResult is { } macro && macro.Item1 is { } savedMacro)
                state.MacroEditorSetter = a => { savedMacro.ActionEnumerable = a; Service.Configuration.Save(); };
            DrawMacro(in state, panelWidth);
        }

        {
            var macroTaskResult = SuggestedMacroTask?.Result;
            var state = new MacroTaskState()
            {
                Type = MacroTaskType.Suggested,
                Exception = SuggestedMacroTask?.Exception,
                Started = SuggestedMacroTask != null,
                Completed = SuggestedMacroTask?.Completed ?? false,
                Actions = macroTaskResult?.Actions,
                State = macroTaskResult?.State,
                Solver = BestMacroSolver,
            };
            DrawMacro(in state, panelWidth);
        }

        if (Service.Configuration.ShowCommunityMacros)
        {
            var macroTaskResult = CommunityMacroTask?.Result;
            var state = new MacroTaskState()
            {
                Type = MacroTaskType.Community,
                Exception = CommunityMacroTask?.Exception,
                Started = CommunityMacroTask != null,
                Completed = CommunityMacroTask?.Completed ?? false,
                Actions = macroTaskResult?.Item1?.Actions,
                MacroName = macroTaskResult?.Item1?.Name,
                MacroUrl = macroTaskResult?.Item1?.Url,
                State = macroTaskResult?.Item2,
            };
            DrawMacro(in state, panelWidth);
        }

        ImGuiHelpers.ScaledDummy(5);

        if (ImGui.Button("View Saved Macros", new(availWidth, 0)))
            Service.Plugin.OpenMacroListWindow();

        if (ImGui.Button("Open in Macro Editor", new(availWidth, 0)))
            Service.Plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.ClientState.LocalPlayer!.StatusList), [], null);
    }

    private void DrawCharacterStats()
    {
        ImGuiUtils.TextCentered("Crafter");

        var level = RecipeData!.ClassJob.GetPlayerLevel();
        {
            var textClassName = RecipeData.ClassJob.GetAbbreviation();
            var textClassSize = AxisFont.CalcTextSize(textClassName);
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
                ImGui.TextUnformatted(levelText);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"CLvl {Gearsets.CalculateCLvl(level)}");
                ImGui.SameLine(0, 3);
            }

            AxisFont.Text(textClassName);

            if (hasSplendorous)
            {
                ImGui.SameLine(0, 3);
                ImGui.Image(SplendorousBadge.ImGuiHandle, new Vector2(imageSize));
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"Splendorous Tool");
            }

            if (hasSpecialist)
            {
                ImGui.SameLine(0, 3);
                ImGui.Image(SpecialistBadge.ImGuiHandle, new Vector2(imageSize), Vector2.Zero, Vector2.One, new(0.99f, 0.97f, 0.62f, 1f));
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"Specialist");
            }

            if (shouldHaveManip)
            {
                ImGui.SameLine(0, 3);
                ImGui.Image(NoManipulationBadge.ImGuiHandle, new Vector2(imageSize));
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"No Manipulation (Missing Job Quest)");
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
                    ImGui.TextUnformatted(unlockText);
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Flag))
                        Service.GameGui.OpenMapWithMapLink(mapPayload);
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip("Open in map");

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
                            RaptureGearsetModule.Instance()->EquipGearset(gearsetId.Value);
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Swap to gearset {gearsetId + 1}");
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
                    ImGui.TextUnformatted(unlockText);
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Flag))
                        Service.GameGui.OpenMapWithMapLink(mapPayload);
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip("Open in map");

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
                    ImGui.TextUnformatted(itemName);
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
                    ImGui.TextUnformatted(statusName);
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
                    using var table = ImRaii.Table("characterStats", 2);
                    if (table)
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("Craftsmanship");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats!.Craftsmanship}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("Control");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats.Control}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("CP");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats.CP}");
                    }
                }
                break;
        }

        using var _spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawRecipeStats()
    {
        ImGuiUtils.TextCentered("Recipe");

        {
            var textStars = new string('★', RecipeData!.Table.Stars);
            var textStarsSize = Vector2.Zero;
            if (!string.IsNullOrEmpty(textStars)) {
                textStarsSize = AxisFont.CalcTextSize(textStars);
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
            ImGui.TextUnformatted(textLevel);
            if (ImGui.IsItemHovered())
                ImGuiUtils.Tooltip($"RLvl {RecipeData.RecipeInfo.RLvl}");

            if (textStarsSize != Vector2.Zero)
            {
                ImGui.SameLine(0, 3);

                // Aligns better
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 1);
                AxisFont.Text(textStars);
            }

            if (isCollectable)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + badgeOffset);
                ImGui.Image(CollectibleBadge.ImGuiHandle, badgeSize);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"Collectible");
            }

            if (isExpert)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + badgeOffset);
                ImGui.Image(ExpertBadge.ImGuiHandle, badgeSize);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"Expert Recipe");
            }
        }

        ImGui.Separator();

        using var table = ImRaii.Table("recipeStats", 2);
        if (table)
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Progress");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxProgress}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Quality");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxQuality}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Durability");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxDurability}");
        }
    }

    private enum MacroTaskType
    {
        Saved,
        Suggested,
        Community
    }
    private record struct MacroTaskState
    {
        public MacroTaskType Type;
        public Exception? Exception;
        public bool Started;
        public bool Completed;
        public IReadOnlyList<ActionType>? Actions;
        public string? MacroName;
        public string? MacroUrl;
        public SimulationState? State;
        public Solver.Solver? Solver;
        public Action<IEnumerable<ActionType>>? MacroEditorSetter;
    }

    public static void DrawSolverTooltip(Solver.Solver solver)
    {
        var tooltip = $"Solver Progress: {solver.ProgressValue:N0} / {solver.ProgressMax:N0}";
        if (solver.ProgressValue > solver.ProgressMax)
            tooltip += $"\n\nThis is taking longer than expected. Check to see if your gear stats are good and the solver settings are adequate.";
        ImGuiUtils.TooltipWrapped(tooltip);
    }

    private void DrawMacro(in MacroTaskState state, float panelWidth)
    {
        var panelTitle = state.Type switch
        {
            MacroTaskType.Saved => "Best Saved Macro",
            MacroTaskType.Suggested => "Suggested Macro",
            MacroTaskType.Community => "Best Community Macro",
            _ => throw new ArgumentOutOfRangeException(nameof(state), "state.Type must have a valid type")
        };

        using var panel = ImRaii2.GroupPanel(panelTitle, panelWidth, out _);
        if (!panel)
            return;

        var stepsAvailWidthOffset = ImGui.GetContentRegionAvail().X - panelWidth;

        var windowHeight = 2 * ImGui.GetFrameHeightWithSpacing();

        if (!state.Started)
        {
            switch (state.Type)
            {
                case MacroTaskType.Saved:
                    throw new InvalidOperationException("Saved macro window should always be started or completed");
                case MacroTaskType.Suggested:
                    {
                        using var _padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding * 2);
                        var size = ImGui.CalcTextSize("Generate") + ImGui.GetStyle().FramePadding * 2;
                        var c = ImGui.GetCursorPos();
                        var availSize = new Vector2(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight);
                        ImGuiUtils.AlignMiddle(size, availSize);
                        if (ImGui.Button("Generate"))
                            CalculateSuggestedMacro();
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.TooltipWrapped("Suggest a way to finish the crafting recipe. " +
                                                      "Results aren't perfect, and levels of success " +
                                                      "can vary wildly depending on the solver's settings.");
                        ImGui.SetCursorPos(c + new Vector2(0, availSize.Y + ImGui.GetStyle().ItemSpacing.Y));
                        break;
                    }
                case MacroTaskType.Community:
                    {
                        using var _padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding * 2);
                        var size = ImGui.CalcTextSize("Search Online") + ImGui.GetStyle().FramePadding * 2;
                        var c = ImGui.GetCursorPos();
                        var availSize = new Vector2(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight);
                        ImGuiUtils.AlignMiddle(size, availSize);
                        if (ImGui.Button("Search Online"))
                            CalculateCommunityMacro();
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.TooltipWrapped("Searches FFXIV Teamcraft to find you the best macro");
                        ImGui.SetCursorPos(c + new Vector2(0, availSize.Y + ImGui.GetStyle().ItemSpacing.Y));
                        break;
                    }
            }
        }
        else if (!state.Completed)
        {
            switch (state.Type)
            {
                case MacroTaskType.Saved:
                    ImGuiUtils.TextMiddleNewLine("Calculating...", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
                    break;
                case MacroTaskType.Suggested:
                    {
                        if (state.Solver is not { } solver)
                            throw new ArgumentNullException(nameof(state), "Solver should not be null");

                        var calcTextSize = ImGui.CalcTextSize("Calculating...");
                        var spacing = ImGui.GetStyle().ItemSpacing.X;
                        var fraction = Math.Clamp((float)solver.ProgressValue / solver.ProgressMax, 0, 1);
                        var progressColors = Colors.GetSolverProgressColors(solver.ProgressStage);

                        var c = ImGui.GetCursorPos();
                        ImGuiUtils.AlignCentered(windowHeight + spacing + calcTextSize.X, ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset);

                        ImGuiUtils.ArcProgress(
                            fraction,
                            windowHeight / 2f + 2,
                            .5f,
                            ImGui.ColorConvertFloat4ToU32(progressColors.Background),
                            ImGui.ColorConvertFloat4ToU32(progressColors.Foreground));
                        if (ImGui.IsItemHovered())
                            DrawSolverTooltip(solver);

                        ImGui.SameLine(0, spacing);

                        ImGuiUtils.AlignMiddle(calcTextSize, new(calcTextSize.X, windowHeight));
                        ImGui.TextUnformatted("Calculating...");
                        ImGui.SetCursorPos(c + new Vector2(0, windowHeight + ImGui.GetStyle().ItemSpacing.Y - 1));
                        break;
                    }
                case MacroTaskType.Community:
                    ImGuiUtils.TextMiddleNewLine("Searching...", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
                    break;
            }
        }
        else if (state.Exception != null)
        {
            ImGui.AlignTextToFramePadding();
            using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                ImGuiUtils.TextCentered("An exception occurred");
            if (ImGuiUtils.ButtonCentered("Copy Error Message"))
                ImGui.SetClipboardText(state.Exception.ToString());
        }
        else if (state.Actions is not { } actions || state.State is not { } simState)
        {
            switch (state.Type)
            {
                case MacroTaskType.Saved:
                    ImGuiUtils.TextMiddleNewLine("You have no macros!", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
                    break;
                case MacroTaskType.Suggested:
                    // Cancelled?
                    break;
                case MacroTaskType.Community:
                    ImGuiUtils.TextMiddleNewLine("No macros found!", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
                    break;
            }
        }
        else
        {
            if (actions.Any(a => a.Category() == ActionCategory.Combo))
                throw new InvalidOperationException("Combo actions should be sanitized away");

            if (state.MacroName is { } macroName)
            {
                using var _ = ImRaii2.TextWrapPos(panelWidth);
                if (state.MacroUrl is { } macroUrl)
                {
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(macroName).X, panelWidth);
                    ImGuiUtils.Hyperlink(macroName, macroUrl, false);
                }
                else
                    ImGuiUtils.TextCentered(macroName, panelWidth);
            }

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
                        if (simState.Progress >= simState.Input.Recipe.MaxProgress && simState.Input.Recipe.MaxQuality > 0)
                        {
                            ImGuiUtils.ArcProgress(
                            (float)simState.Quality / simState.Input.Recipe.MaxQuality,
                            progressHeight / 2f,
                            .5f,
                            ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                            ImGui.GetColorU32(Colors.Quality));
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip($"Quality: {simState.Quality} / {simState.Input.Recipe.MaxQuality}");
                        }
                        else
                        {
                            ImGuiUtils.ArcProgress(
                            (float)simState.Progress / simState.Input.Recipe.MaxProgress,
                            progressHeight / 2f,
                            .5f,
                            ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                            ImGui.GetColorU32(Colors.Progress));
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip($"Progress: {simState.Progress} / {simState.Input.Recipe.MaxProgress}");
                        }
                    }
                    else
                    {
                        ImGuiUtils.ArcProgress(
                        (float)simState.Progress / simState.Input.Recipe.MaxProgress,
                            miniRowHeight / 2f,
                            .5f,
                            ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                            ImGui.GetColorU32(Colors.Progress));
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Progress: {simState.Progress} / {simState.Input.Recipe.MaxProgress}");

                        ImGui.SameLine(0, spacing);
                        ImGuiUtils.ArcProgress(
                        (float)simState.Quality / simState.Input.Recipe.MaxQuality,
                            miniRowHeight / 2f,
                            .5f,
                            ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                            ImGui.GetColorU32(Colors.Quality));
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Quality: {simState.Quality} / {simState.Input.Recipe.MaxQuality}");
                        ImGuiUtils.ArcProgress((float)simState.Durability / simState.Input.Recipe.MaxDurability,
                        miniRowHeight / 2f,
                            .5f,
                            ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                            ImGui.GetColorU32(Colors.Durability));
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Remaining Durability: {simState.Durability} / {simState.Input.Recipe.MaxDurability}");

                        ImGui.SameLine(0, spacing);
                        ImGuiUtils.ArcProgress(
                        (float)simState.CP / simState.Input.Stats.CP,
                            miniRowHeight / 2f,
                            .5f,
                            ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                            ImGui.GetColorU32(Colors.CP));
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Remaining CP: {simState.CP} / {simState.Input.Stats.CP}");
                    }
                }

                ImGui.TableNextColumn();
                {
                    if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit, miniRowHeight))
                        Service.Plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.ClientState.LocalPlayer!.StatusList), actions, state.MacroEditorSetter);
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip("Open in Macro Editor");
                    if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Paste, miniRowHeight))
                        Service.Plugin.CopyMacro(actions);
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip("Copy to Clipboard");
                }

                ImGui.TableNextColumn();
                {
                    var itemsPerRow = (int)MathF.Floor((ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset + spacing) / (miniRowHeight + spacing));
                    var itemCount = actions.Count;
                    for (var i = 0; i < itemsPerRow * 2; i++)
                    {
                        if (i % itemsPerRow != 0)
                            ImGui.SameLine(0, spacing);
                        if (i < itemCount)
                        {
                            var shouldShowMore = i + 1 == itemsPerRow * 2 && i + 1 < itemCount;
                            if (!shouldShowMore)
                            {
                                ImGui.Image(actions[i].GetIcon(RecipeData!.ClassJob).ImGuiHandle, new(miniRowHeight));
                                if (ImGui.IsItemHovered())
                                    ImGuiUtils.Tooltip(actions[i].GetName(RecipeData!.ClassJob));
                            }
                            else
                            {
                                var amtMore = itemCount - itemsPerRow * 2;
                                var pos = ImGui.GetCursorPos();
                                ImGui.Image(actions[i].GetIcon(RecipeData!.ClassJob).ImGuiHandle, new(miniRowHeight), default, Vector2.One, new(1, 1, 1, .5f));
                                if (ImGui.IsItemHovered())
                                    ImGuiUtils.Tooltip($"{actions[i].GetName(RecipeData!.ClassJob)}\nand {amtMore} more");
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
    }

    private static void DrawRequiredStatsTable(int current, int required)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(current, required);

        using var table = ImRaii.Table("requiredStats", 2);
        if (table)
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Current");
            ImGui.TableNextColumn();
            ImGui.TextColored(new(0, 1, 0, 1), $"{current}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Required");
            ImGui.TableNextColumn();
            ImGui.TextColored(new(1, 0, 0, 1), $"{required}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("You need");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{required - current}");
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
        var location = WorldToMap2(new(level.X, level.Z), level.Map.Value!);

        return (ResolveNpcResidentName(level.Object.Row), territory, location, new(level.Territory.Row, level.Map.Row, location.X, location.Y));
    }

    private static Vector2 WorldToMap2(Vector2 worldCoordinates, ExdSheets.Map map)
    {
        return MapUtil.WorldToMap(worldCoordinates, map.OffsetX, map.OffsetY, map.SizeFactor);
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

    private void CalculateSavedMacro()
    {
        SavedMacroTask?.Cancel();
        SavedMacroTask = new(token =>
        {
            var input = new SimulationInput(CharacterStats!, RecipeData!.RecipeInfo);
            var state = new SimulationState(input);
            var config = Service.Configuration.RecipeNoteSolverConfig;
            var mctsConfig = new MCTSConfig(config);
            var simulator = new SimulatorNoRandom();
            List<Macro> macros = new(Service.Configuration.Macros);

            token.ThrowIfCancellationRequested();

            if (macros.Count == 0)
                return (null, null);
            var bestSaved = macros
                .Select(macro =>
                {
                    var (score, outState) = CommunityMacros.CommunityMacro.CalculateScore(macro.Actions, simulator, in state, in mctsConfig);
                    return (macro, outState, score);
                })
                .MaxBy(m => m.score);

            token.ThrowIfCancellationRequested();

            return (bestSaved.macro, bestSaved.outState);
        });
        SavedMacroTask.Start();
    }

    private void CalculateSuggestedMacro()
    {
        SuggestedMacroTask?.Cancel();
        SuggestedMacroTask = new(token =>
        {
            var input = new SimulationInput(CharacterStats!, RecipeData!.RecipeInfo);
            var state = new SimulationState(input);
            var config = Service.Configuration.RecipeNoteSolverConfig;

            token.ThrowIfCancellationRequested();

            var solver = new Solver.Solver(config, state) { Token = token };
            solver.OnLog += Log.Debug;
            BestMacroSolver = solver;
            solver.Start();
            var solution = solver.GetTask().GetAwaiter().GetResult();

            token.ThrowIfCancellationRequested();

            return solution;
        });
        SuggestedMacroTask.Start();
    }

    public void CalculateCommunityMacro()
    {
        CommunityMacroTask?.Cancel();
        CommunityMacroTask = new(token =>
        {
            var input = new SimulationInput(CharacterStats!, RecipeData!.RecipeInfo);
            var state = new SimulationState(input);
            var config = Service.Configuration.RecipeNoteSolverConfig;
            var mctsConfig = new MCTSConfig(config);
            var simulator = new SimulatorNoRandom();
            var macros = Service.CommunityMacros.RetrieveRotations(input.Recipe.RLvl, token).GetAwaiter().GetResult();

            token.ThrowIfCancellationRequested();

            if (macros.Count == 0)
                return (null, null);
            var bestSaved = macros
                .Select(macro =>
                {
                    var (score, outState) = CommunityMacros.CommunityMacro.CalculateScore(macro.Actions, simulator, in state, in mctsConfig);
                    return (macro, outState, score);
                })
                .MaxBy(m => m.score);

            token.ThrowIfCancellationRequested();

            return (bestSaved.macro, bestSaved.outState);
        });
        CommunityMacroTask.Start();
    }

    public void Dispose()
    {
        SavedMacroTask?.Dispose();
        SuggestedMacroTask?.Dispose();
        CommunityMacroTask?.Dispose();
        Service.WindowSystem.RemoveWindow(this);
        AxisFont?.Dispose();
    }
}
