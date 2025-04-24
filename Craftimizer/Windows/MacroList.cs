using Craftimizer.Plugin;
using Craftimizer.Utils;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

namespace Craftimizer.Windows;

public sealed class MacroList : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    public CharacterStats? CharacterStats { get; private set; }
    public RecipeData? RecipeData { get; private set; }

    private IReadOnlyList<Macro> Macros => Service.Configuration.Macros;
    private Dictionary<Macro, SimulationState> MacroStateCache { get; } = [];

    public MacroList() : base("Craftimizer Macro List", WindowFlags, false)
    {
        RefreshSearch();

        Macro.OnMacroChanged += OnMacroChanged;
        Configuration.OnMacroListChanged += OnMacroListChanged;

        CollapsedCondition = ImGuiCond.Appearing;
        Collapsed = false;

        SizeConstraints = new() { MinimumSize = new(465, 520), MaximumSize = new(float.PositiveInfinity) };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => Service.CraftimizerPlugin.OpenSettingsTab("General"),
                ShowTooltip = () => ImGuiUtils.Tooltip("Open Settings")
            },
            new() {
                Icon = FontAwesomeIcon.Heart,
                IconOffset = new(2, 1),
                Click = _ => Util.OpenLink(Plugin.CraftimizerPlugin.SupportLink),
                ShowTooltip = () => ImGuiUtils.Tooltip("Support me on Ko-fi!")
            }
        ];

        Service.WindowSystem.AddWindow(this);
    }

    public override bool DrawConditions()
    {
        return Service.ClientState.LocalPlayer != null;
    }

    public override void PreDraw()
    {
        var oldCharacterStats = CharacterStats;
        var oldRecipeData = RecipeData;

        (CharacterStats, RecipeData, _) = Service.CraftimizerPlugin.GetDefaultStats();

        if (oldCharacterStats != CharacterStats || oldRecipeData != RecipeData)
            RecalculateStats();
    }

    public override void Draw()
    {
        DrawSearchBar();
        using var group = ImRaii.Child("macros", new(-1, -1));
        if (sortedMacros.Count > 0)
        {
            var width = ImGui.GetContentRegionAvail().X;
            var macros = new List<Macro>(sortedMacros);
            for(var i = 0; i < macros.Count; ++i)
            {
                var pos = ImGui.GetCursorPos();
                DrawMacro(macros[i]);
                ImGui.SetCursorPos(pos);
                ImGui.InvisibleButton($"###macroButton{i}", ImGui.GetItemRectSize());
                if (isUnsorted)
                {
                    using (var _source = ImRaii.DragDropSource(ImGuiDragDropFlags.SourceNoDisableHover))
                    {
                        if (_source)
                        {
                            ImGuiExtras.SetDragDropPayload("macroListItem", i);
                            DrawMacro(macros[i], width);
                        }
                    }
                    using (var _target = ImRaii.DragDropTarget())
                    {
                        if (_target)
                        {
                            if (ImGuiExtras.AcceptDragDropPayload("macroListItem", out int j))
                                Service.Configuration.MoveMacro(j, i);
                        }
                    }
                }
            }
        }
        else
        {
            var text1 = "You have no macros! Create one by opening";
            var text2 = "the Macro Editor here or from the Crafting Log.";
            var text3 = "Open Crafting Log";
            var text4 = "Open Macro Editor";
            var buttonRowWidth = ImGui.CalcTextSize(text3).X + ImGui.CalcTextSize(text4).X + ImGui.GetStyle().ItemSpacing.X * 5;
            var size = new Vector2(
                Math.Max(
                    Math.Max(ImGui.CalcTextSize(text1).X, ImGui.CalcTextSize(text2).X),
                    buttonRowWidth
                ),
                ImGui.GetTextLineHeightWithSpacing() * 2 + ImGui.GetFrameHeight()
            );
            ImGuiUtils.AlignMiddle(size);
            using var child = ImRaii.Child("##macroMessage", size);
            ImGuiUtils.TextCentered(text1);
            ImGuiUtils.TextCentered(text2);
            ImGuiUtils.AlignCentered(buttonRowWidth);
            if (ImGui.Button(text3))
                Service.CraftimizerPlugin.OpenCraftingLog();
            ImGui.SameLine();
            if (ImGui.Button(text4))
                OpenEditor(null);
        }
    }

    private string searchText = string.Empty;
    private List<Macro> sortedMacros = null!;
    private bool isUnsorted = true;
    private void DrawSearchBar()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputTextWithHint("##search", "Search", ref searchText, 100))
            RefreshSearch();
    }

    private void DrawMacro(Macro macro, float width = -1)
    {
        width = width < 0 ? ImGui.GetContentRegionAvail().X : width;

        var windowHeight = 2 * ImGui.GetFrameHeightWithSpacing();

        if (macro.Actions.Any(a => a.Category() == ActionCategory.Combo))
            throw new InvalidOperationException("Combo actions should be sanitized away");

        var stateNullable = GetMacroState(macro);

        using var panel = ImRaii2.GroupPanel(macro.Name, width - ImGui.GetStyle().ItemSpacing.X * 2, out var availWidth);
        var stepsAvailWidthOffset = width - availWidth;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var miniRowHeight = (windowHeight - spacing) / 2f;

        using var table = ImRaii.Table("table", stateNullable.HasValue ? 3 : 2, ImGuiTableFlags.BordersInnerV);
        if (table)
        {
            if (stateNullable.HasValue)
                ImGui.TableSetupColumn("stats", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("actions", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("steps", ImGuiTableColumnFlags.WidthStretch, 0);

            ImGui.TableNextRow(ImGuiTableRowFlags.None, windowHeight);
            if (stateNullable is { } state)
            {
                ImGui.TableNextColumn();
                if (Service.Configuration.ShowOptimalMacroStat)
                {
                    var progressHeight = windowHeight;
                    if (state.Progress >= state.Input.Recipe.MaxProgress && state.Input.Recipe.MaxQuality > 0)
                    {
                        ImGuiUtils.ArcProgress(
                        (float)state.Quality / state.Input.Recipe.MaxQuality,
                        progressHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Quality));
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Quality: {state.Quality} / {state.Input.Recipe.MaxQuality}");
                    }
                    else
                    {
                        ImGuiUtils.ArcProgress(
                        (float)state.Progress / state.Input.Recipe.MaxProgress,
                        progressHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Progress));
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Progress: {state.Progress} / {state.Input.Recipe.MaxProgress}");
                    }
                }
                else
                {
                    ImGuiUtils.ArcProgress(
                        (float)state.Progress / state.Input.Recipe.MaxProgress,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Progress));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip($"Progress: {state.Progress} / {state.Input.Recipe.MaxProgress}");

                    ImGui.SameLine(0, spacing);
                    ImGuiUtils.ArcProgress(
                        (float)state.Quality / state.Input.Recipe.MaxQuality,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Quality));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip($"Quality: {state.Quality} / {state.Input.Recipe.MaxQuality}");

                    ImGuiUtils.ArcProgress((float)state.Durability / state.Input.Recipe.MaxDurability,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.Durability));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip($"Remaining Durability: {state.Durability} / {state.Input.Recipe.MaxDurability}");

                    ImGui.SameLine(0, spacing);
                    ImGuiUtils.ArcProgress(
                        (float)state.CP / state.Input.Stats.CP,
                        miniRowHeight / 2f,
                        .5f,
                        ImGui.GetColorU32(ImGuiCol.TableBorderLight),
                        ImGui.GetColorU32(Colors.CP));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip($"Remaining CP: {state.CP} / {state.Input.Stats.CP}");
                }
            }

            ImGui.TableNextColumn();
            {
                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit, miniRowHeight))
                    OpenEditor(macro);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("Open in Macro Editor");
                ImGui.SameLine(0, spacing);
                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.PencilAlt, miniRowHeight))
                    ShowRenamePopup(macro);
                DrawRenamePopup(macro);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("Rename");

                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Paste, miniRowHeight))
                    MacroCopy.Copy(macro.Actions);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("Copy to Clipboard");
                ImGui.SameLine(0, spacing);
                using (var _disabled = ImRaii.Disabled(!ImGui.GetIO().KeyShift))
                {
                    if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Trash, miniRowHeight))
                        Service.Configuration.RemoveMacro(macro);
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGuiUtils.Tooltip("Delete (Hold Shift)");
            }

            ImGui.TableNextColumn();
            {
                var itemsPerRow = (int)MathF.Floor((ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset + spacing * 2) / (miniRowHeight + spacing));
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
                                ImGuiUtils.Tooltip(macro.Actions[i].GetName(RecipeData!.ClassJob));
                        }
                        else
                        {
                            var amtMore = itemCount - itemsPerRow * 2;
                            var pos = ImGui.GetCursorPos();
                            ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).ImGuiHandle, new(miniRowHeight), default, Vector2.One, new(1, 1, 1, .5f));
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip($"{macro.Actions[i].GetName(RecipeData!.ClassJob)}\nand {amtMore} more");
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

    private string popupMacroName = string.Empty;
    private Macro? popupMacro;
    private void ShowRenamePopup(Macro macro)
    {
        ImGui.OpenPopup($"##renamePopup-{macro.GetHashCode()}");
        popupMacro = macro;
        popupMacroName = macro.Name;
        ImGui.SetNextWindowPos(ImGui.GetMousePos() - new Vector2(ImGui.CalcItemWidth() * .25f, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
    }

    private void DrawRenamePopup(Macro macro)
    {
        using var popup = ImRaii.Popup($"##renamePopup-{macro.GetHashCode()}");
        if (popup)
        {
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();
            ImGui.SetNextItemWidth(ImGui.CalcItemWidth());
            if (ImGui.InputTextWithHint($"##setName", "Name", ref popupMacroName, 100, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrWhiteSpace(popupMacroName))
                {
                    popupMacro!.Name = popupMacroName;
                    Service.Configuration.Save();
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    private void RecalculateStats()
    {
        MacroStateCache.Clear();
    }

    private void RefreshSearch()
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            sortedMacros = new(Macros);
            isUnsorted = true;
            return;
        }
        isUnsorted = false;
        var matcher = new FuzzyMatcher(searchText.ToLowerInvariant(), MatchMode.FuzzyParts);
        var query = Macros.AsParallel().Select(i => (Item: i, Score: matcher.Matches(i.Name.ToLowerInvariant())))
            .Where(t => t.Score > 0)
            .OrderByDescending(t => t.Score)
            .Select(t => t.Item);
        sortedMacros = [.. query];
    }

    private void OpenEditor(Macro? macro)
    {
        var stats = Service.CraftimizerPlugin.GetDefaultStats();
        Service.CraftimizerPlugin.OpenMacroEditor(stats.Character, stats.Recipe, stats.Buffs, null, macro?.Actions ?? Enumerable.Empty<ActionType>(), macro != null ? (actions => { macro.ActionEnumerable = actions; Service.Configuration.Save(); }) : null);
    }

    private void OnMacroChanged(Macro macro)
    {
        MacroStateCache.Remove(macro);
    }

    private void OnMacroListChanged()
    {
        RefreshSearch();
    }

    private SimulationState? GetMacroState(Macro macro)
    {
        if (CharacterStats == null || RecipeData == null)
            return null;

        if (MacroStateCache.TryGetValue(macro, out var state))
            return state;

        state = new SimulationState(new(CharacterStats, RecipeData.RecipeInfo));
        var sim = new RotationSimulatorNoRandom();
        (_, state, _) = sim.ExecuteMultiple(state, macro.Actions);
        return MacroStateCache[macro] = state;
    }

    public void Dispose()
    {
        Macro.OnMacroChanged -= OnMacroChanged;
        Configuration.OnMacroListChanged -= OnMacroListChanged;

        Service.WindowSystem.RemoveWindow(this);
    }
}
