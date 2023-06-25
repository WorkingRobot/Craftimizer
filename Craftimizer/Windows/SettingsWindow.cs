using Craftimizer.Simulator;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Craftimizer.Plugin.Windows;

public class SettingsWindow : Window
{
    private static Configuration Config => Service.Configuration;

    public SettingsWindow() : base("Craftimizer")
    {
        Service.WindowSystem.AddWindow(this);

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Size = SizeConstraints.Value.MinimumSize;
    }

    public override void Draw()
    {
        var val = Config.OverrideUncraftability;
        if (ImGui.Checkbox("Override Uncraftability Warning", ref val))
            Config.OverrideUncraftability = val;
    }
}
