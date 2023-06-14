using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Craftimizer;

internal class ImGuiUtils
{
    static List<(Vector2 Min, Vector2 Max)> GroupPanelLabelStack = new();

    static void BeginGroupPanel(string name, Vector2 size)
    {
        ImGui.BeginGroup();

        var cursorPos = ImGui.GetCursorScreenPos();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0.0f, 0.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0.0f, 0.0f));

        var frameHeight = ImGui.GetFrameHeight();
        ImGui.BeginGroup();

        var effectiveSize = size;
        if (size.X < 0.0f)
            effectiveSize.X = ImGui.GetContentRegionAvail().X;
        else
            effectiveSize.X = size.X;
        ImGui.Dummy(new Vector2(effectiveSize.X, 0.0f));

        ImGui.Dummy(new Vector2(frameHeight* 0.5f, 0.0f));
        ImGui.SameLine(0.0f, 0.0f);
        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(frameHeight* 0.5f, 0.0f));
        ImGui.SameLine(0.0f, 0.0f);
        ImGui.TextUnformatted(name);
        var labelMin = ImGui.GetItemRectMin();
        var labelMax = ImGui.GetItemRectMax();
        ImGui.SameLine(0.0f, 0.0f);
        ImGui.Dummy(new Vector2(0f, frameHeight + itemSpacing.Y));
        ImGui.BeginGroup();

        ImGui.PopStyleVar(2);

        //ImGui.GetCurrentWindow()->ContentRegionRect.Max.x -= frameHeight * 0.5f;
        //ImGui.GetCurrentWindow()->WorkRect.Max.x          -= frameHeight * 0.5f;
        //ImGui.GetCurrentWindow()->InnerRect.Max.x         -= frameHeight * 0.5f;
        //ImGui.GetCurrentWindow()->Size.x                   -= frameHeight;

        var itemWidth = ImGui.CalcItemWidth();
        ImGui.PushItemWidth(MathF.Max(0.0f, itemWidth - frameHeight));

        GroupPanelLabelStack.Add((labelMin, labelMax));
    }

    void EndGroupPanel()
    {
        ImGui.PopItemWidth();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0.0f, 0.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0.0f, 0.0f));

        var frameHeight = ImGui.GetFrameHeight();

        ImGui.EndGroup();

        ImGui.EndGroup();

        ImGui.SameLine(0.0f, 0.0f);
        ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0.0f));
        ImGui.Dummy(new Vector2(0f, frameHeight - frameHeight * 0.5f - itemSpacing.Y));

        ImGui.EndGroup();

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();
        var labelRect = GroupPanelLabelStack[^1];
        GroupPanelLabelStack.RemoveAt(GroupPanelLabelStack.Count - 1);

        var halfFrame = new Vector2(frameHeight * 0.25f, frameHeight) * 0.5f;
        (Vector2 Min, Vector2 Max) frameRect = (itemMin + halfFrame, itemMax - new Vector2(halfFrame.X, 0.0f));
        labelRect.Min.X -= itemSpacing.X;
        labelRect.Max.X += itemSpacing.X;
        for (var i = 0; i < 4; ++i)
        {
            switch (i)
            {
                // left half-plane
                case 0: ImGui.PushClipRect(new Vector2(float.NegativeInfinity), new Vector2(labelRect.Min.X, float.PositiveInfinity), true); break;
                // right half-plane
                case 1: ImGui.PushClipRect(new Vector2(labelRect.Max.X, float.NegativeInfinity), new Vector2(float.PositiveInfinity), true); break;
                // top
                case 2: ImGui.PushClipRect(new Vector2(labelRect.Min.X, float.NegativeInfinity), new Vector2(labelRect.Max.X, labelRect.Min.Y), true); break;
                // bottom
                case 3: ImGui.PushClipRect(new Vector2(labelRect.Min.X, labelRect.Max.Y), new Vector2(labelRect.Max.X, float.PositiveInfinity), true); break;
            }

            ImGui.GetWindowDrawList().AddRect(
                frameRect.Min, frameRect.Max,
                ImGui.GetColorU32(ImGuiCol.Border),
                halfFrame.X);

            ImGui.PopClipRect();
        }

        ImGui.PopStyleVar(2);

        //ImGui.GetCurrentWindow()->ContentRegionRect.Max.x += frameHeight * 0.5f;
        //ImGui.GetCurrentWindow()->WorkRect.Max.x          += frameHeight * 0.5f;
        //ImGui.GetCurrentWindow()->InnerRect.Max.x         += frameHeight * 0.5f;
        //ImGui.GetCurrentWindow()->Size.x += frameHeight;

        ImGui.Dummy(new Vector2(0.0f, 0.0f));

        ImGui.EndGroup();
    }
}
