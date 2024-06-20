using Craftimizer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using ImPlotNET;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Craftimizer.Plugin;

internal static class ImGuiUtils
{
    private static readonly Stack<(Vector2 Min, Vector2 Max, float TopPadding)> GroupPanelLabelStack = new();

    // Adapted from https://github.com/ocornut/imgui/issues/1496#issuecomment-655048353
    // width = -1 -> size to parent
    // width = 0 -> size to content
    // returns available width (better since it accounts for the right side padding)
    // ^ only useful if width = -1
    public static float BeginGroupPanel(string name, float width)
    {
        ImGui.PushID(name);

        // container group
        ImGui.BeginGroup();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        var frameHeight = ImGui.GetFrameHeight();
        width = width < 0 ? ImGui.GetContentRegionAvail().X - (2 * itemSpacing.X) : width;
        var fullWidth = width > 0 ? width + (2 * itemSpacing.X) : 0;
        {
            using var noPadding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);
            using var noSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            // inner group
            ImGui.BeginGroup();
            ImGui.Dummy(new Vector2(fullWidth, 0));
            ImGui.Dummy(new Vector2(itemSpacing.X, 0)); // shifts next group by is.x
            ImGui.SameLine(0, 0);

            // label group
            ImGui.BeginGroup();
            if (ImGui.CalcTextSize(name, true).X == 0)
            {
                GroupPanelLabelStack.Push(default);
                ImGui.Dummy(new Vector2(0f, itemSpacing.Y)); // shifts content by is.y
            }
            else
            {
                ImGui.Dummy(new Vector2(frameHeight / 2, 0)); // shifts text by fh/2
                ImGui.SameLine(0, 0);
                var textFrameHeight = ImGui.GetFrameHeight();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(name);
                GroupPanelLabelStack.Push((ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), textFrameHeight / 2f)); // push rect to stack
                ImGui.SameLine(0, 0);
                ImGui.Dummy(new Vector2(0f, textFrameHeight + itemSpacing.Y)); // shifts content by fh + is.y
            }

            // content group
            ImGui.BeginGroup();
        }

        return width;
    }

    public static void EndGroupPanel()
    {
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        {
            using var noPadding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);
            using var noSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            // content group
            ImGui.EndGroup();

            // label group
            ImGui.EndGroup();

            ImGui.SameLine(0, 0);
            // shifts full size by is (for rect placement)
            ImGui.Dummy(new(itemSpacing.X, 0));
            ImGui.Dummy(new(0, itemSpacing.Y * 2)); // * 2 for some reason (otherwise the bottom is too skinny)

            // inner group
            ImGui.EndGroup();

            var (labelMin, labelMax, labelPadding) = GroupPanelLabelStack.Pop();

            var innerMin = ImGui.GetItemRectMin();
            var innerMax = ImGui.GetItemRectMax();
            // If there was actual text
            if (labelMax.X != labelMin.X)
            {
                innerMin += new Vector2(0, labelPadding);

                // add itemspacing padding on the label's sides
                labelMin.X -= itemSpacing.X / 2;
                labelMax.X += itemSpacing.X / 2;
            }
            for (var i = 0; i < 4; ++i)
            {
                var (minClip, maxClip) = i switch
                {
                    0 => (new Vector2(float.NegativeInfinity), new Vector2(labelMin.X, float.PositiveInfinity)),
                    1 => (new Vector2(labelMax.X, float.NegativeInfinity), new Vector2(float.PositiveInfinity)),
                    2 => (new Vector2(labelMin.X, float.NegativeInfinity), new Vector2(labelMax.X, labelMin.Y)),
                    3 => (new Vector2(labelMin.X, labelMax.Y), new Vector2(labelMax.X, float.PositiveInfinity)),
                    _ => (Vector2.Zero, Vector2.Zero)
                };

                ImGui.PushClipRect(minClip, maxClip, true);
                ImGui.GetWindowDrawList().AddRect(
                    innerMin, innerMax,
                    ImGui.GetColorU32(ImGuiCol.Border),
                    itemSpacing.X);
                ImGui.PopClipRect();
            }

            ImGui.Dummy(Vector2.Zero);
        }

        ImGui.EndGroup();

        ImGui.PopID();
    }

    private static Vector2 UnitCircle(float theta)
    {
        var (s, c) = MathF.SinCos(theta);
        // SinCos positive y is downwards, but we want it upwards for ImGui
        return new Vector2(c, -s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) =>
        MathF.FusedMultiplyAdd(b - a, t, a);

    private readonly record struct ArcEdge(float Angle, Vector2 Point)
    {
        public ArcEdge(float angle) : this(angle, UnitCircle(angle)) { }
    }

    private static void ArcSegment(Vector2 o, ArcEdge prev, ArcEdge cur, ArcEdge? next, float radius, float ratio, uint color)
    {
        var d = ImGui.GetWindowDrawList();

        d.PathLineTo(o + cur.Point * radius);
        d.PathLineTo(o + prev.Point * radius);
        d.PathLineTo(o + prev.Point * radius * ratio);
        d.PathLineTo(o + cur.Point * radius * ratio);
        if (next is { } nextValue)
            d.PathLineTo(o + nextValue.Point * radius);
        d.PathFillConvex(color);
    }

    public static void Arc(float startAngle, float endAngle, float radius, float ratio, uint backgroundColor, uint filledColor, bool addDummy = true)
    {
        // Fix normals when drawing (for antialiasing)
        if (startAngle > endAngle)
            (startAngle, endAngle) = (endAngle, startAngle);

        // Origin of circle
        var offset = ImGui.GetCursorScreenPos() + new Vector2(radius);

        // Number of segments to draw
        var segments = ImGui.GetWindowDrawList()._CalcCircleAutoSegmentCount(radius);
        // Angle between each segment
        var incrementAngle = MathF.Tau / segments;
        // Whether the arc is a full circle (no background or all background)
        var isFullCircle = (endAngle - startAngle) % MathF.Tau == 0;

        var end = new ArcEdge(endAngle);
        var prev = new ArcEdge(startAngle);
        for (var i = 1; i <= segments; ++i)
        {
            var cur = new ArcEdge(startAngle + incrementAngle * i);
            var next = new ArcEdge(startAngle + incrementAngle * (i + 1));

            // full segment is background
            if (prev.Angle >= end.Angle)
            {
                // don't overlap with the first segment
                if (i == segments && !isFullCircle)
                    ArcSegment(offset, prev, cur, null, radius, ratio, backgroundColor);
                else
                    ArcSegment(offset, prev, cur, next, radius, ratio, backgroundColor);
            }
            // segment is partially filled
            else if (cur.Angle > end.Angle && !isFullCircle)
            {
                // we split the drawing in two
                ArcSegment(offset, prev, end, null, radius, ratio, filledColor);
                if (i == segments)
                    ArcSegment(offset, end, cur, null, radius, ratio, backgroundColor);
                else
                    ArcSegment(offset, end, cur, next, radius, ratio, backgroundColor);
                // set the previous segment to end
                cur = end;
            }
            // full segment is filled
            else
            {
                // if the next segment will be partially filled, the next segment will be the end
                if (next.Angle > end.Angle && !isFullCircle)
                    ArcSegment(offset, prev, cur, end, radius, ratio, filledColor);
                else
                    ArcSegment(offset, prev, cur, next, radius, ratio, filledColor);
            }
            prev = cur;
        }

        if (addDummy)
            ImGui.Dummy(new Vector2(radius * 2));
    }

    public static void ArcProgress(float value, float radius, float ratio, uint backgroundColor, uint filledColor)
    {
        Arc(MathF.PI / 2, MathF.PI / 2 - MathF.Tau * Math.Clamp(value, 0, 1), radius, ratio, backgroundColor, filledColor);
    }

    public sealed class ViolinData
    {
        public struct Point(float x, float y, float y2)
        {
            public float X = x, Y = y, Y2 = y2;
        }

        public ReadOnlySpan<Point> Data => (DataArray ?? []).AsSpan();
        private Point[]? DataArray { get; set; }
        public readonly float Min;
        public readonly float Max;

        public ViolinData(IEnumerable<int> samples, float min, float max, int resolution, double bandwidth)
        {
            Min = min;
            Max = max;
            bandwidth *= Max - Min;
            var samplesList = samples.AsParallel().Select(s => (double)s).ToArray();
            _ = Task.Run(() => {
                var s = Stopwatch.StartNew();
                var data = ParallelEnumerable.Range(0, resolution + 1)
                    .Select(n => Lerp(min, max, n / (float)resolution))
                    .Select(n => (n, (float)KernelDensity.EstimateGaussian(n, bandwidth, samplesList)))
                    .Select(n => new Point(n.n, n.Item2, -n.Item2));
                // ParallelQuery doesn't support [.. data] correctly. The plots look very wrong.
#pragma warning disable IDE0305 // Simplify collection initialization
                DataArray = data.ToArray();
#pragma warning restore IDE0305 // Simplify collection initialization
                s.Stop();
                Log.Debug($"Violin plot processing took {s.Elapsed.TotalMilliseconds:0.00}ms");
            });
        }
    }

    public static void ViolinPlot(in ViolinData data, Vector2 size)
    {
        using var padding = ImRaii2.PushStyle(ImPlotStyleVar.PlotPadding, Vector2.Zero);
        using var plotBg = ImRaii2.PushColor(ImPlotCol.PlotBg, Vector4.Zero);
        using var fill = ImRaii2.PushColor(ImPlotCol.Fill, Vector4.One.WithAlpha(.5f));

        using var plot = ImRaii2.Plot("##violin", size, ImPlotFlags.CanvasOnly | ImPlotFlags.NoInputs | ImPlotFlags.NoChild | ImPlotFlags.NoFrame);
        if (plot)
        {
            ImPlot.SetupAxes(null, null, ImPlotAxisFlags.NoDecorations, ImPlotAxisFlags.NoDecorations | ImPlotAxisFlags.AutoFit);
            ImPlot.SetupAxisLimits(ImAxis.X1, data.Min, data.Max, ImPlotCond.Always);
            ImPlot.SetupFinish();

            if (data.Data is { } points && !points.IsEmpty)
            {
                unsafe
                {
                    var label_id = stackalloc byte[] { (byte)'\0' };
                    fixed (ViolinData.Point* p = points)
                    {
                        ImPlotNative.ImPlot_PlotShaded_FloatPtrFloatPtrFloatPtr(label_id, &p->X, &p->Y, &p->Y2, points.Length, ImPlotShadedFlags.None, 0, sizeof(ViolinData.Point));
                    }
                }
            }
        }
    }

    private sealed class SearchableComboData<T> where T : class
    {
        public readonly ImmutableArray<T> items;
        public List<T> filteredItems;
        public T selectedItem;
        public string input;
        public bool wasTextActive;
        public bool wasPopupActive;
        public CancellationTokenSource? cts;
        public Task? task;

        private readonly Func<T, string> getString;

        public SearchableComboData(IEnumerable<T> items, T selectedItem, Func<T, string> getString)
        {
            this.items = items.ToImmutableArray();
            filteredItems = [selectedItem];
            this.selectedItem = selectedItem;
            this.getString = getString;
            input = GetString(selectedItem);
        }

        public void SetItem(T selectedItem)
        {
            if (this.selectedItem != selectedItem)
            {
                input = GetString(selectedItem);
                this.selectedItem = selectedItem;
            }
        }

        public string GetString(T item) => getString(item);

        public void Filter()
        {
            cts?.Cancel();
            var inp = input;
            cts = new();
            var token = cts.Token;
            task = Task.Run(() => FilterTask(inp, token), token)
                .ContinueWith(t =>
            {
                if (cts.IsCancellationRequested)
                    return;

                try
                {
                    t.Exception!.Flatten().Handle(ex => ex is TaskCanceledException or OperationCanceledException);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Filtering recipes failed");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void FilterTask(string input, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                filteredItems = [.. items];
                return;
            }
            var matcher = new FuzzyMatcher(input.ToLowerInvariant(), MatchMode.FuzzyParts);
            var query = items.AsParallel().Select(i => (Item: i, Score: matcher.Matches(getString(i).ToLowerInvariant())))
                .Where(t => t.Score > 0)
                .OrderByDescending(t => t.Score)
                .Select(t => t.Item);
            token.ThrowIfCancellationRequested();
            filteredItems = [.. query];
        }
    }
    private static readonly Dictionary<uint, object> ComboData = [];

    private static SearchableComboData<T> GetComboData<T>(uint comboKey, IEnumerable<T> items, T selectedItem, Func<T, string> getString) where T : class =>
        (SearchableComboData<T>)(
            ComboData.TryGetValue(comboKey, out var data)
            ? data
            : ComboData[comboKey] = new SearchableComboData<T>(items, selectedItem, getString));

    // https://github.com/ocornut/imgui/issues/718#issuecomment-1563162222
    public static bool SearchableCombo<T>(string id, ref T selectedItem, IEnumerable<T> items, ImFontPtr selectableFont, float width, Func<T, string> getString, Func<T, string> getId, Action<T> draw) where T : class
    {
        var comboKey = ImGui.GetID(id);
        var data = GetComboData(comboKey, items, selectedItem, getString);
        data.SetItem(selectedItem);

        using var pushId = ImRaii.PushId(id);

        width = width == 0 ? ImGui.GetContentRegionAvail().X : width;
        var availableSpace = Math.Min(ImGui.GetContentRegionAvail().X, width);
        ImGui.SetNextItemWidth(availableSpace);
        var isInputTextEnterPressed = ImGui.InputText("##input", ref data.input, 256, ImGuiInputTextFlags.EnterReturnsTrue);
        var min = ImGui.GetItemRectMin();
        var size = ImGui.GetItemRectSize();
        size.X = Math.Min(size.X, availableSpace);

        var isInputTextActivated = ImGui.IsItemActivated();

        if (isInputTextActivated)
        {
            ImGui.SetNextWindowPos(min - ImGui.GetStyle().WindowPadding);
            ImGui.OpenPopup("##popup");
            data.wasTextActive = false;
        }

        using (var popup = ImRaii.Popup("##popup", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            if (popup)
            {
                data.wasPopupActive = true;

                if (isInputTextActivated)
                {
                    ImGui.SetKeyboardFocusHere(0);
                    data.Filter();
                }
                ImGui.SetNextItemWidth(size.X);
                if (ImGui.InputText("##input_popup", ref data.input, 256))
                    data.Filter();
                var isActive = ImGui.IsItemActive();
                if (!isActive && data.wasTextActive && ImGui.IsKeyPressed(ImGuiKey.Enter))
                    isInputTextEnterPressed = true;
                data.wasTextActive = isActive;

                using (var scrollingRegion = ImRaii.Child("scrollingRegion", new Vector2(size.X, size.Y * 10), false, ImGuiWindowFlags.HorizontalScrollbar))
                {
                    T? _selectedItem = default;
                    var height = ImGui.GetTextLineHeight();
                    var r = ListClip(data.filteredItems, height, t =>
                    {
                        var name = getString(t);
                        using (var selectFont = ImRaii.PushFont(selectableFont))
                        {
                            if (ImGui.Selectable($"##{getId(t)}"))
                            {
                                _selectedItem = t;
                                return true;
                            }
                        }
                        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X / 2f);
                        draw(t);
                        return false;
                    });
                    if (r)
                    {
                        selectedItem = _selectedItem!;
                        data.SetItem(selectedItem);
                        ImGui.CloseCurrentPopup();
                        return true;
                    }
                }

                if (isInputTextEnterPressed || ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    if (isInputTextEnterPressed && data.filteredItems.Count > 0)
                    {
                        selectedItem = data.filteredItems[0];
                        data.SetItem(selectedItem);
                    }
                    ImGui.CloseCurrentPopup();
                    return true;
                }
            }
            else
            {
                if (data.wasPopupActive)
                {
                    data.wasPopupActive = false;
                    data.input = getString(selectedItem);
                }
            }
        }

        return false;
    }

    private static bool ListClip<T>(IReadOnlyList<T> data, float lineHeight, Predicate<T> func)
    {
        ImGuiListClipperPtr imGuiListClipperPtr;
        unsafe
        {
            imGuiListClipperPtr = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        }
        try
        {
            imGuiListClipperPtr.Begin(data.Count, lineHeight);
            while (imGuiListClipperPtr.Step())
            {
                for (var i = imGuiListClipperPtr.DisplayStart; i <= imGuiListClipperPtr.DisplayEnd; i++)
                {
                    if (i >= data.Count)
                        return false;

                    if (i >= 0)
                    {
                        if (func(data[i]))
                            return true;
                    }
                }
            }
            return false;
        }
        finally
        {
            imGuiListClipperPtr.End();
            imGuiListClipperPtr.Destroy();
        }
    }

    public static bool InputTextMultilineWithHint(string label, string hint, ref string input, int maxLength, Vector2 size, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None, ImGuiInputTextCallback? callback = null, IntPtr user_data = default)
    {
        const ImGuiInputTextFlags Multiline = (ImGuiInputTextFlags)(1 << 26);
        return ImGuiExtras.InputTextEx(label, hint, ref input, maxLength, size, flags | Multiline, callback, user_data);
    }

    private static Vector2 GetIconSize(FontAwesomeIcon icon)
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        return ImGui.CalcTextSize(icon.ToIconString());
    }

    private static void DrawCenteredIcon(FontAwesomeIcon icon, Vector2 offset, Vector2 size, bool isDisabled = false)
    {
        var iconSize = GetIconSize(icon);

        float scale;
        Vector2 iconOffset;
        if (iconSize.X > iconSize.Y)
        {
            scale = size.X / iconSize.X;
            iconOffset = new(0, (size.Y - (iconSize.Y * scale)) / 2f);
        }
        else if (iconSize.Y > iconSize.X)
        {
            scale = size.Y / iconSize.Y;
            iconOffset = new((size.X - (iconSize.X * scale)) / 2f, 0);
        }
        else
        {
            scale = size.X / iconSize.X;
            iconOffset = Vector2.Zero;
        }

        ImGui.GetWindowDrawList().AddText(UiBuilder.IconFont, UiBuilder.IconFont.FontSize * scale, offset + iconOffset, ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled), icon.ToIconString());
    }

    public static bool IconButtonSquare(FontAwesomeIcon icon, float size = -1)
    {
        var ret = false;

        var buttonSize = new Vector2(size == -1 ? ImGui.GetFrameHeight() : size);
        var pos = ImGui.GetCursorScreenPos();
        var spacing = new Vector2(ImGui.GetStyle().FramePadding.Y);

        if (ImGui.Button($"###{icon.ToIconString()}", buttonSize))
            ret = true;

        var isDisabled = ImGuiExtras.GetItemFlags().HasFlag(ImGuiItemFlags.Disabled);
        DrawCenteredIcon(icon, pos + spacing, buttonSize - spacing * 2, isDisabled);

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
    public static unsafe void Hyperlink(string text, string url, bool underline = true)
    {
        ImGui.TextUnformatted(text);
        if (underline)
            UnderlineLastItem(*ImGui.GetStyleColorVec4(ImGuiCol.Text));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            var urlWithoutScheme = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                urlWithoutScheme = uri.Host + (string.Equals(uri.PathAndQuery, "/", StringComparison.Ordinal) ? string.Empty : uri.PathAndQuery);
            Tooltip(urlWithoutScheme);
        }
    }

    public static void Tooltip(string text)
    {
        using var _font = ImRaii.PushFont(UiBuilder.DefaultFont);
        using var _tooltip = ImRaii.Tooltip();
        ImGui.TextUnformatted(text);
    }

    public static void TooltipWrapped(string text, float width = 300)
    {
        using var _font = ImRaii.PushFont(UiBuilder.DefaultFont);
        using var _tooltip = ImRaii.Tooltip();
        using var _wrap = ImRaii2.TextWrapPos(width);
        ImGui.TextUnformatted(text);
    }

    public static void TextWrappedTo(string text, float wrapPosX = default, float basePosX = default)
    {
        var font = ImGui.GetFont();

        var currentPos = ImGui.GetCursorPosX();

        if (basePosX == default)
            basePosX = ImGui.GetCursorStartPos().X;

        float currentWrapWidth;
        if (wrapPosX == default)
            currentWrapWidth = ImGui.GetContentRegionAvail().X;
        else
            currentWrapWidth = wrapPosX - currentPos;

        var textBuf = text.AsSpan();
        var lineSize = font.CalcWordWrapPositionA(1, textBuf, currentWrapWidth) ?? textBuf.Length;
        var lineBuf = textBuf[..lineSize];
        ImGui.TextUnformatted(lineBuf.ToString());
        var remainingBuf = textBuf[lineSize..].TrimStart();

        if (!remainingBuf.IsEmpty)
        {
            ImGui.SetCursorPosX(basePosX);
            using (ImRaii2.TextWrapPos(wrapPosX))
                ImGui.TextWrapped(remainingBuf.ToString());
        }
    }

    public static void AlignCentered(float width, float availWidth = default)
    {
        if (availWidth == default)
            availWidth = ImGui.GetContentRegionAvail().X;
        if (availWidth > width)
            ImGui.SetCursorPosX(ImGui.GetCursorPos().X + (availWidth - width) / 2);
    }

    public static void AlignRight(float width, float availWidth = default)
    {
        if (availWidth == default)
            availWidth = ImGui.GetContentRegionAvail().X;
        if (availWidth > width)
            ImGui.SetCursorPosX(ImGui.GetCursorPos().X + availWidth - width);
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

    public static void TextRight(string text, float availWidth = default)
    {
        AlignRight(ImGui.CalcTextSize(text).X, availWidth);
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

    public static float GetFontSize(this IFontHandle font)
    {
        using (font.Push())
            return ImGui.GetFontSize();
    }

    public static Vector2 CalcTextSize(this IFontHandle font, string text)
    {
        using (font.Push())
            return ImGui.CalcTextSize(text);
    }

    public static void Text(this IFontHandle font, string text)
    {
        using (font.Push())
            ImGui.TextUnformatted(text);
    }
}
