using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using ImPlotNET;
using System;
using System.Numerics;

namespace Craftimizer.Plugin;

public static class ImRaii2
{
    private struct EndUnconditionally(Action endAction, bool success) : ImRaii.IEndObject, IDisposable
    {
        private Action EndAction { get; } = endAction;

        public bool Success { get; } = success;

        public bool Disposed { get; private set; } = false;

        public void Dispose()
        {
            if (!Disposed)
            {
                EndAction();
                Disposed = true;
            }
        }
    }

    private struct EndConditionally(Action endAction, bool success) : ImRaii.IEndObject, IDisposable
    {
        public bool Success { get; } = success;

        public bool Disposed { get; private set; } = false;

        private Action EndAction { get; } = endAction;

        public void Dispose()
        {
            if (!Disposed)
            {
                if (Success)
                {
                    EndAction();
                }

                Disposed = true;
            }
        }
    }

    public static ImRaii.IEndObject GroupPanel(string name, float width, out float internalWidth)
    {
        internalWidth = ImGuiUtils.BeginGroupPanel(name, width);
        return new EndUnconditionally(ImGuiUtils.EndGroupPanel, true);
    }

    public static ImRaii.IEndObject Plot(string title_id, Vector2 size, ImPlotFlags flags)
    {
        return new EndConditionally(new Action(ImPlot.EndPlot), ImPlot.BeginPlot(title_id, size, flags));
    }

    public static ImRaii.IEndObject PushStyle(ImPlotStyleVar idx, Vector2 val)
    {
        ImPlot.PushStyleVar(idx, val);
        return new EndUnconditionally(ImPlot.PopStyleVar, true);
    }

    public static ImRaii.IEndObject PushStyle(ImPlotStyleVar idx, float val)
    {
        ImPlot.PushStyleVar(idx, val);
        return new EndUnconditionally(ImPlot.PopStyleVar, true);
    }

    public static ImRaii.IEndObject PushColor(ImPlotCol idx, Vector4 col)
    {
        ImPlot.PushStyleColor(idx, col);
        return new EndUnconditionally(ImPlot.PopStyleColor, true);
    }

    public static ImRaii.IEndObject TextWrapPos(float wrap_local_pos_x)
    {
        ImGui.PushTextWrapPos(wrap_local_pos_x);
        return new EndUnconditionally(ImGui.PopTextWrapPos, true);
    }
}
