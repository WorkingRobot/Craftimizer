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
using System.Diagnostics;

namespace Craftimizer.Plugin.Windows;

public class SettingsWindow : Window
{
    private static Configuration Config => Service.Configuration;

    public SettingsWindow() : base("Craftimizer Settings")
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
        bool val;
        float valF;
        int valI;

        ImGuiUtils.BeginGroupPanel("General");

        var isDirty = false;

        val = Config.OverrideUncraftability;
        if (ImGui.Checkbox("Override uncraftability warning", ref val))
        {
            Config.OverrideUncraftability = val;
            isDirty = true;
        }

        val = Config.HideUnlearnedActions;
        if (ImGui.Checkbox("Show only learned actions", ref val))
        {
            Config.HideUnlearnedActions = val;
            isDirty = true;
        }

        val = Config.ConditionRandomness;
        if (ImGui.Checkbox("Condition randomness", ref val))
        {
            Config.ConditionRandomness = val;
            isDirty = true;
        }

        ImGuiUtils.EndGroupPanel();

        ImGuiUtils.BeginGroupPanel("Solver");

        ImGui.TextWrapped("Credit to altosock's Craftingway for the original algorithm");
        if (ImGui.Button("Open Craftingway"))
            Process.Start(new ProcessStartInfo { FileName = "https://craftingway.app", UseShellExecute = true });

        ImGuiHelpers.ScaledDummy(10);

        var config = Config.SolverConfig;
        var isSolverDirty = false;

        valI = config.Iterations;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputInt("Iterations", ref valI))
        {
            config = config with { Iterations = valI };
            isSolverDirty = true;
        }

        valF = config.ScoreStorageThreshold;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputFloat("Score Storage Threshold", ref valF))
        {
            config = config with { ScoreStorageThreshold = valF };
            isSolverDirty = true;
        }

        valF = config.MaxScoreWeightingConstant;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputFloat("Score Weighting Constant", ref valF))
        {
            config = config with { MaxScoreWeightingConstant = valF };
            isSolverDirty = true;
        }

        valF = config.ExplorationConstant;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputFloat("Exploration Constant", ref valF))
        {
            config = config with { ExplorationConstant = valF };
            isSolverDirty = true;
        }

        valI = config.MaxStepCount;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputInt("Max Step Count", ref valI))
        {
            config = config with { MaxStepCount = valI };
            isSolverDirty = true;
        }

        if (ImGui.Button("Reset to defaults"))
        {
            config = new();
            isSolverDirty = true;
        }

        if (isSolverDirty)
        {
            Config.SolverConfig = config;
            isDirty = true;
        }

        ImGuiUtils.EndGroupPanel();

        if (isDirty)
            Config.Save();
    }
}
