using Dalamud.Interface;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Craftimizer.Plugin;

internal static class ImGuiUtils
{
    private static readonly Stack<(Vector2 Min, Vector2 Max)> GroupPanelLabelStack = new();

    // Adapted from https://github.com/ocornut/imgui/issues/1496#issuecomment-655048353
    public static void BeginGroupPanel(float width = -1, bool addPadding = true)
    {
        ImGui.BeginGroup();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        var frameHeight = ImGui.GetFrameHeight();

        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(width < 0 ? ImGui.GetContentRegionAvail().X : width, 0));
        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0));
        ImGui.SameLine(0, 0);

        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0));
        GroupPanelLabelStack.Push((ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));
        ImGui.SameLine(0, 0);
        ImGui.Dummy(new Vector2(0f, frameHeight * (addPadding ? 1 : .5f) + itemSpacing.Y));

        ImGui.BeginGroup();

        ImGui.PopStyleVar(2);

        ImGui.PushItemWidth(MathF.Max(0, ImGui.CalcItemWidth() - frameHeight));
    }

    public static void BeginGroupPanel(string name, float width = -1, bool addPadding = true)
    {
        ImGui.BeginGroup();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        var frameHeight = ImGui.GetFrameHeight();

        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(width < 0 ? ImGui.GetContentRegionAvail().X : width, 0));
        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0));
        ImGui.SameLine(0, 0);

        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0));
        ImGui.SameLine(0, 0);
        ImGui.TextUnformatted(name);
        GroupPanelLabelStack.Push((ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));
        ImGui.SameLine(0, 0);
        ImGui.Dummy(new Vector2(0f, frameHeight * (addPadding ? 1 : .5f) + itemSpacing.Y));

        ImGui.BeginGroup();

        ImGui.PopStyleVar(2);

        ImGui.PushItemWidth(MathF.Max(0, ImGui.CalcItemWidth() - frameHeight));
    }

    public static void EndGroupPanel()
    {
        ImGui.PopItemWidth();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        var frameHeight = ImGui.GetFrameHeight();

        ImGui.EndGroup();

        ImGui.EndGroup();

        ImGui.SameLine(0, 0);
        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0));
        ImGui.Dummy(new Vector2(0f, frameHeight * 0.5f - itemSpacing.Y));

        ImGui.EndGroup();

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();
        var labelRect = GroupPanelLabelStack.Pop();

        var halfFrame = new Vector2(frameHeight * 0.25f, frameHeight) * 0.5f;
        (Vector2 Min, Vector2 Max) frameRect = (itemMin + halfFrame, itemMax - new Vector2(halfFrame.X, 0));
        labelRect.Min.X -= itemSpacing.X;
        labelRect.Max.X += itemSpacing.X;
        for (var i = 0; i < 4; ++i)
        {
            var (minClip, maxClip) = i switch
            {
                0 => (new Vector2(float.NegativeInfinity), new Vector2(labelRect.Min.X, float.PositiveInfinity)),
                1 => (new Vector2(labelRect.Max.X, float.NegativeInfinity), new Vector2(float.PositiveInfinity)),
                2 => (new Vector2(labelRect.Min.X, float.NegativeInfinity), new Vector2(labelRect.Max.X, labelRect.Min.Y)),
                3 => (new Vector2(labelRect.Min.X, labelRect.Max.Y), new Vector2(labelRect.Max.X, float.PositiveInfinity)),
                _ => (Vector2.Zero, Vector2.Zero)
            };

            ImGui.PushClipRect(minClip, maxClip, true);
            ImGui.GetWindowDrawList().AddRect(
                frameRect.Min, frameRect.Max,
                ImGui.GetColorU32(ImGuiCol.Border),
                halfFrame.X);
            ImGui.PopClipRect();
        }

        ImGui.PopStyleVar(2);

        ImGui.Dummy(Vector2.Zero);

        ImGui.EndGroup();
    }

    private static Vector2 UnitCircle(float theta)
    {
        var (s, c) = MathF.SinCos(theta);
        // SinCos positive y is downwards, but we want it upwards for ImGui
        return new Vector2(c, -s);
    }

    private static float Lerp(float a, float b, float t) =>
        MathF.FusedMultiplyAdd(b - a, t, a);

    private static void ArcSegment(Vector2 o, Vector2 prev, Vector2 cur, Vector2? next, float radius, float ratio, uint color)
    {
        var d = ImGui.GetWindowDrawList();

        d.PathLineTo(o + cur * radius);
        d.PathLineTo(o + prev * radius);
        d.PathLineTo(o + prev * radius * ratio);
        d.PathLineTo(o + cur * radius * ratio);
        if (next is { } nextValue)
            d.PathLineTo(o + nextValue * radius);
        d.PathFillConvex(color);
    }

    public static void Arc(float startAngle, float endAngle, float radius, float ratio, uint backgroundColor, uint filledColor, bool addDummy = true)
    {
        // Fix normals when drawing (for antialiasing)
        if (startAngle > endAngle)
            (startAngle, endAngle) = (endAngle, startAngle);

        var offset = ImGui.GetCursorScreenPos() + new Vector2(radius);

        var segments = ImGui.GetWindowDrawList()._CalcCircleAutoSegmentCount(radius * 2);
        var incrementAngle = MathF.Tau / segments;
        var isFullCircle = (endAngle - startAngle) % MathF.Tau == 0;

        var prevA = startAngle;
        var prev = UnitCircle(prevA);
        for (var i = 1; i <= segments; ++i)
        {
            var a = startAngle + incrementAngle * i;
            var cur = UnitCircle(a);

            var nextA = a + incrementAngle;
            var next = UnitCircle(nextA);

            // full segment is background
            if (prevA >= endAngle)
            {
                // don't overlap with the first segment
                if (i == segments && !isFullCircle)
                    ArcSegment(offset, prev, cur, null, radius, ratio, backgroundColor);
                else
                    ArcSegment(offset, prev, cur, next, radius, ratio, backgroundColor);
            }
            // segment is partially filled
            else if (a > endAngle && !isFullCircle)
            {
                // we split the drawing in two
                var end = UnitCircle(endAngle);
                ArcSegment(offset, prev, end, null, radius, ratio, filledColor);
                ArcSegment(offset, end, cur, next, radius, ratio, backgroundColor);
                // set the previous segment to endAngle
                a = endAngle;
                cur = end;
            }
            // full segment is filled
            else
            {
                // if the next segment will be partially filled, the next segment will be the endAngle
                if (nextA > endAngle && !isFullCircle)
                {
                    var end = UnitCircle(endAngle);
                    ArcSegment(offset, prev, cur, end, radius, ratio, filledColor);
                }
                else
                    ArcSegment(offset, prev, cur, next, radius, ratio, filledColor);
            }
            prevA = a;
            prev = cur;
        }

        if (addDummy)
            ImGui.Dummy(new Vector2(radius * 2));
    }

    public static void ArcProgress(float value, float radiusInner, float radiusOuter, uint backgroundColor, uint filledColor)
    {
        Arc(MathF.PI / 2, MathF.PI / 2 - MathF.Tau * Math.Clamp(value, 0, 1), radiusInner, radiusOuter, backgroundColor, filledColor);
    }

    public static bool IconButtonSized(FontAwesomeIcon icon, Vector2 size)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var ret = ImGui.Button(icon.ToIconString(), size);
        ImGui.PopFont();
        return ret;
    }

    // https://gist.github.com/dougbinks/ef0962ef6ebe2cadae76c4e9f0586c69#file-imguiutils-h-L219
    private static void UnderlineLastItem(Vector4 color)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        min.Y = max.Y;
        ImGui.GetWindowDrawList().AddLine(min, max, ImGui.ColorConvertFloat4ToU32(color), 1);
    }

    // https://gist.github.com/dougbinks/ef0962ef6ebe2cadae76c4e9f0586c69#file-imguiutils-h-L228
    public static unsafe void Hyperlink(string text, string url)
    {
        ImGui.TextUnformatted(text);
        UnderlineLastItem(*ImGui.GetStyleColorVec4(ImGuiCol.Text));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            ImGui.SetTooltip("Open in Browser");
        }
    }

    public static void AlignCentered(float width, float availWidth = default)
    {
        if (availWidth == default)
            availWidth = ImGui.GetContentRegionAvail().X;
        if (availWidth > width)
            ImGui.SetCursorPosX(ImGui.GetCursorPos().X + (availWidth - width) / 2);
    }

    public static void AlignMiddle(Vector2 size, Vector2 availSize = default)
    {
        if (availSize == default)
            availSize = ImGui.GetContentRegionAvail();
        if (availSize.X > size.X)
            ImGui.SetCursorPosX(ImGui.GetCursorPos().X + (availSize.X - size.X) / 2);
        if (availSize.Y > size.Y)
            ImGui.SetCursorPosY(ImGui.GetCursorPos().Y + (availSize.Y - size.Y) / 2);
    }

    // https://stackoverflow.com/a/67855985
    public static void TextCentered(string text, float availWidth = default)
    {
        AlignCentered(ImGui.CalcTextSize(text).X, availWidth);
        ImGui.TextUnformatted(text);
    }

    public static void TextMiddle(string text, Vector2 availSize = default)
    {
        AlignMiddle(ImGui.CalcTextSize(text), availSize);
        ImGui.TextUnformatted(text);
    }

    public static void TextMiddleNewLine(string text, Vector2 availSize)
    {
        if (availSize == default)
            availSize = ImGui.GetContentRegionAvail();
        var c = ImGui.GetCursorPos();
        AlignMiddle(ImGui.CalcTextSize(text), availSize);
        ImGui.TextUnformatted(text);
        ImGui.SetCursorPos(c + new Vector2(0, availSize.Y + ImGui.GetStyle().ItemSpacing.Y - 1));
    }

    public static bool ButtonCentered(string text, Vector2 buttonSize = default)
    {
        var buttonWidth = buttonSize.X;
        if (buttonSize == default)
            buttonWidth = ImGui.CalcTextSize(text).X + ImGui.GetStyle().FramePadding.X * 2;
        AlignCentered(buttonWidth);
        return ImGui.Button(text, buttonSize);
    }
}
