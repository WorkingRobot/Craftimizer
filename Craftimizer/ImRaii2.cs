using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using System.Numerics;

namespace Craftimizer.Plugin;

public static class ImRaii2
{
    // Custom ref structs for each UI element. 
    // This eliminates the need for allocating 'Action' delegates and boxing.

    public ref struct GroupPanelDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            ImGuiUtils.EndGroupPanel();
            disposed = true;
        }

        public static implicit operator bool(GroupPanelDisposable _) => true;
    }

    public static GroupPanelDisposable GroupPanel(string name, float width, out float internalWidth)
    {
        internalWidth = ImGuiUtils.BeginGroupPanel(name, width);
        return new GroupPanelDisposable();
    }

    public ref struct PlotDisposable(bool success)
    {
        public bool Success { get; } = success;
        private bool disposed = false;

        public void Dispose()
        {
            if (disposed) return;
            if (Success)
            {
                ImPlot.EndPlot();
            }
            disposed = true;
        }

        // Allows you to do: using var plot = ImRaii2.Plot(...); if (plot) { ... }
        public static implicit operator bool(PlotDisposable d) => d.Success;
    }

    public static PlotDisposable Plot(string title_id, Vector2 size, ImPlotFlags flags)
    {
        return new PlotDisposable(ImPlot.BeginPlot(title_id, size, flags));
    }

    public ref struct ImPlotStyleDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            ImPlot.PopStyleVar();
            disposed = true;
        }
    }

    public static ImPlotStyleDisposable PushStyle(ImPlotStyleVar idx, Vector2 val)
    {
        ImPlot.PushStyleVar(idx, val);
        return new ImPlotStyleDisposable();
    }

    public static ImPlotStyleDisposable PushStyle(ImPlotStyleVar idx, float val)
    {
        ImPlot.PushStyleVar(idx, val);
        return new ImPlotStyleDisposable();
    }

    public ref struct ImPlotColorDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            ImPlot.PopStyleColor();
            disposed = true;
        }
    }

    public static ImPlotColorDisposable PushColor(ImPlotCol idx, Vector4 col)
    {
        ImPlot.PushStyleColor(idx, col);
        return new ImPlotColorDisposable();
    }

    public ref struct TextWrapPosDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            ImGui.PopTextWrapPos();
            disposed = true;
        }
    }

    public static TextWrapPosDisposable TextWrapPos(float wrap_local_pos_x)
    {
        ImGui.PushTextWrapPos(wrap_local_pos_x);
        return new TextWrapPosDisposable();
    }
}
