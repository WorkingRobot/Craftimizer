using Craftimizer.Simulator;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace Craftimizer.Plugin.Windows;

public sealed partial class SimulatorWindow : Window, IDisposable
{
    private readonly record struct SynthDrawParams
    {
        public float LeftColumn { get; init; }
        public float RightColumn { get; init; }
        public float LeftText { get; init; }
        public float RightText { get; init; }
        public float Total { get; init; }
    }

    private SynthDrawParams CalculateSynthDrawParams()
    {
        var sidePadding = ImGui.GetFrameHeight() / 2;
        var separatorTextWidth = ImGui.CalcTextSize(" / ").X;
        var itemSpacing = ImGui.GetStyle().ItemSpacing.X;

        var leftDigits = (int)MathF.Floor(MathF.Log10(Input.Recipe.MaxDurability) + 1);
        var leftTextWidth = ImGui.CalcTextSize(new string('0', leftDigits)).X;
        var leftWidth = DurabilityBarSize.X + sidePadding + itemSpacing * 2 + separatorTextWidth + leftTextWidth * 2;


        var rightDigits = (int)MathF.Floor(MathF.Log10(Math.Max(Math.Max(Input.Recipe.MaxProgress, Input.Recipe.MaxQuality), Input.Stats.CP)) + 1);
        var rightTextWidth = ImGui.CalcTextSize(new string('0', rightDigits)).X;
        var rightWidth = ProgressBarSize.X + sidePadding + itemSpacing * 2 + separatorTextWidth + rightTextWidth * 2;

        return new()
        {
            LeftColumn = leftWidth,
            LeftText = leftTextWidth,
            RightColumn = rightWidth,
            RightText = rightTextWidth,
            Total = leftWidth + rightWidth + itemSpacing
        };
    }

    // Generic Progress Bar
    private static void DrawSynthProgress(string name, int current, int max, Vector2 size, Vector4 color, float textWidth)
    {
        ImGuiUtils.BeginGroupPanel(name);

        DrawProgressBar(current, max, size, color);

        var w = ImGui.GetStyle().ItemSpacing.X;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));

        ImGui.SameLine(0, textWidth - ImGui.CalcTextSize($"{current}").X + w);
        var adjustedHeight = ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f);
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted($"{current}");

        ImGui.SameLine();
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted(" / ");

        ImGui.SameLine(0, textWidth - ImGui.CalcTextSize($"{max}").X);
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted($"{max}");

        ImGui.PopStyleVar();

        ImGuiUtils.EndGroupPanel();
    }

    // HQ% / Collectability Bar (has no fractional bar to indicate max)
    private static void DrawSynthBar(string name, int current, int max, string text, Vector2 size, Vector4 color, float textWidth)
    {
        ImGuiUtils.BeginGroupPanel(name);

        DrawProgressBar(current, max, size, color);

        var w = ImGui.GetStyle().ItemSpacing.X;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));

        var totalWidth = textWidth * 2 + ImGui.CalcTextSize(" / ").X;

        ImGui.SameLine(0, totalWidth - ImGui.CalcTextSize(text).X + w);
        var adjustedHeight = ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f);
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted(text);

        ImGui.PopStyleVar();

        ImGuiUtils.EndGroupPanel();
    }

    // Condition "Bar" Circle (always 100%, is a circle)
    private static void DrawSynthCircle(string name, string text, Vector2 size, Vector4 color, Vector2 otherProgressSize, float textWidth)
    {
        ImGuiUtils.BeginGroupPanel(name);

        var w = ImGui.GetStyle().ItemSpacing.X;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));

        var contentWidth = size.X + w + ImGui.CalcTextSize(text).X;
        var totalWidth = otherProgressSize.X + w + textWidth * 2 + ImGui.CalcTextSize(" / ").X;

        ImGui.Dummy(default);
        ImGui.SameLine(0, (totalWidth - contentWidth) / 2);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Math.Max(size.X, size.Y));
        DrawProgressBar(1, 1, size, color);
        ImGui.PopStyleVar();
        ImGui.SameLine(0, w);
        var adjustedHeight = ImGui.GetCursorPosY() - ((ImGui.GetFrameHeight() - ImGui.GetFontSize()) / 2f);
        ImGui.SetCursorPosY(adjustedHeight);
        ImGui.TextUnformatted(text);

        ImGui.PopStyleVar();

        ImGuiUtils.EndGroupPanel();
    }

    public static void DrawAllProgressTooltips(SimulationState state)
    {
        DrawProgressBarTooltip(state.Progress, state.Input.Recipe.MaxProgress, ProgressColor);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Progress: {state.Progress} / {state.Input.Recipe.MaxProgress}");
        DrawProgressBarTooltip(state.Quality, state.Input.Recipe.MaxQuality, QualityColor);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Quality: {state.Quality} / {state.Input.Recipe.MaxQuality}");
        DrawProgressBarTooltip(state.Durability, state.Input.Recipe.MaxDurability, DurabilityColor);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Durability: {state.Durability} / {state.Input.Recipe.MaxDurability}");
        DrawProgressBarTooltip(state.CP, state.Input.Stats.CP, CPColor);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"CP: {state.CP} / {state.Input.Stats.CP}");
    }

    private static void DrawProgressBarTooltip(int progress, int maxProgress, Vector4 color) =>
        DrawProgressBar(progress, maxProgress, TooltipProgressBarSize, color);

    private static void DrawProgressBar(int progress, int maxProgress, Vector2 size, Vector4 color, string overlay = "")
    {
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(Math.Clamp((float)progress / maxProgress, 0f, 1f), size, overlay);
        ImGui.PopStyleColor();
    }
}
