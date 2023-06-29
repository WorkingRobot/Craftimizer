using Dalamud.Interface.Windowing;
using System;
using System.Diagnostics;

namespace Craftimizer.Plugin.Windows;

public sealed partial class SimulatorWindow : Window, IDisposable
{
    private TimeSpan FrameTime { get; set; }
    private Stopwatch Stopwatch { get; } = new();

    public override void PreDraw()
    {
        Stopwatch.Restart();

        base.PreDraw();
    }

    public override void PostDraw()
    {
        Stopwatch.Stop();
        FrameTime = Stopwatch.Elapsed;

        base.PostDraw();
    }
}
