using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using ImGuiNET;
using System.Numerics;
using System.Diagnostics;
using System;

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

    private static void DrawOption(string label, string tooltip, bool val, Action<bool> setter, ref bool isDirty)
    {
        if (ImGui.Checkbox(label, ref val))
        {
            setter(val);
            isDirty = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private static void DrawOption(string label, string tooltip, int val, Action<int> setter, ref bool isDirty)
    {
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputInt(label, ref val))
        {
            setter(val);
            isDirty = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private static void DrawOption(string label, string tooltip, float val, Action<float> setter, ref bool isDirty)
    {
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputFloat(label, ref val))
        {
            setter(val);
            isDirty = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    public override void Draw()
    {
        ImGuiUtils.BeginGroupPanel("General");

        var isDirty = false;

        DrawOption(
            "Override uncraftability warning",
            "Allow simulation for crafts that otherwise wouldn't\n" +
            "be able to be crafted with your current gear",
            Config.OverrideUncraftability,
            v => Config.OverrideUncraftability = v,
            ref isDirty
        );

        DrawOption(
            "Show only learned actions",
            "Don't show crafting actions that haven't been\n" +
            "learned yet with your current job on the simulator sidebar",
            Config.HideUnlearnedActions,
            v => Config.HideUnlearnedActions = v,
            ref isDirty
        );

        DrawOption(
            "Hide combo actions",
            "Don't show combo actions on the simulator sidebar",
            Config.HideCombos,
            v => Config.HideCombos = v,
            ref isDirty
        );

        DrawOption(
            "Condition randomness",
            "Allows the simulator condition to fluctuate randomly like a real craft.\nTurns off when generating a macro.",
            Config.ConditionRandomness,
            v => Config.ConditionRandomness = v,
            ref isDirty
        );

        ImGuiUtils.EndGroupPanel();

        ImGuiUtils.BeginGroupPanel("Solver");

        ImGui.Text("Credit to altosock's ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("Craftingway", "https://craftingway.app");
        ImGui.SameLine(0, 0);
        ImGui.Text(" for the original algorithm");

        ImGuiHelpers.ScaledDummy(5);

        var config = Config.SolverConfig;
        var isSolverDirty = false;

        DrawOption(
            "Iterations",
            "The total number of iterations to run per crafting step.\n" +
            "Higher values require more computational power. Higher values\n" +
            "also may decrease variance, so other values should be tweaked\n" +
            "as necessary to get a more favorable outcome.",
            config.Iterations,
            v => config = config with { Iterations = v },
            ref isSolverDirty
        );

        DrawOption(
            "Score Storage Threshold",
            "If a craft achieves this certain arbitrary score, the solver will\n" +
            "throw away all other possible combinations in favor of that one.\n" +
            "Only change this value if you absolutely know what you're doing.",
            config.ScoreStorageThreshold,
            v => config = config with { ScoreStorageThreshold = v },
            ref isSolverDirty
        );

        DrawOption(
            "Score Weighting Constant",
            "A constant ranging from 0 to 1 that configures how the solver\n" +
            "scores and picks paths to travel to next. A value of 0 means\n" +
            "actions will be chosen based on their average outcome, whereas\n" +
            "1 uses their best outcome achieved so far.",
            config.MaxScoreWeightingConstant,
            v => config = config with { MaxScoreWeightingConstant = v },
            ref isSolverDirty
        );

        DrawOption(
            "Exploration Constant",
            "A constant that decides how often the solver will explore new,\n" +
            "possibly good paths. If this value is too high,\n" +
            "moves will mostly be decided at random.",
            config.ExplorationConstant,
            v => config = config with { ExplorationConstant = v },
            ref isSolverDirty
        );

        DrawOption(
            "Max Step Count",
            "The maximum number of crafting steps; this is generally the only\n" +
            "setting you should change, and it should be set to around 5 steps\n" +
            "more than what you'd expect. If this value is too low, the solver\n" +
            "won't learn much per iteration; too high and it will waste time\n" +
            "on useless extra steps.",
            config.MaxStepCount,
            v => config = config with { MaxStepCount = v },
            ref isSolverDirty
        );

        DrawOption(
            "Max Rollout Step Count",
            "The maximum number of crafting steps every iteration can consider.\n" +
            "Decreasing this value can have unintended side effects. Only change\n" +
            "this value if you absolutely know what you're doing.",
            config.MaxRolloutStepCount,
            v => config = config with { MaxRolloutStepCount = v },
            ref isSolverDirty
        );

        DrawOption(
            "Fork Count",
            "Split the number of iterations across different solvers. In general,\n" +
            "you should increase this value to at least the number of cores in\n" +
            $"your system (FYI, you have {Environment.ProcessorCount} cores) to\n" +
            "attain the most speedup. The higher the number, the more chance you\n" +
            "have of finding a better local maximum; this concept similar but\n" +
            "not equivalent to the exploration constant.",
            config.ForkCount,
            v => config = config with { ForkCount = v },
            ref isSolverDirty
        );

        DrawOption(
            "Furcated Action Count",
            "On every craft step, pick this many top solutions and use them as\n" +
            "the input for the next craft step. For best results, use Fork Count / 2\n" +
            "and add about 1 or 2 more if needed.",
            config.FurcatedActionCount,
            v => config = config with { FurcatedActionCount = v },
            ref isSolverDirty
        );

        DrawOption(
            "Strict Actions",
            "When finding the next possible actions to execute, use a heuristic\n" +
            "to restrict which actions to attempt taking. This results in a much\n" +
            "better macro at the cost of not finding an extremely creative one.",
            config.StrictActions,
            v => config = config with { StrictActions = v },
            ref isSolverDirty
        );

        ImGuiUtils.BeginGroupPanel("Score Weights");
        ImGui.TextWrapped("All values must add up to 1. Otherwise, the Score Storage Threshold must be changed.");
        ImGuiHelpers.ScaledDummy(10);

        DrawOption(
            "Progress",
            "Amount of weight to give to the craft's progress.",
            config.ScoreProgressBonus,
            v => config = config with { ScoreProgressBonus = v },
            ref isSolverDirty
        );

        DrawOption(
            "Quality",
            "Amount of weight to give to the craft's quality.",
            config.ScoreQualityBonus,
            v => config = config with { ScoreQualityBonus = v },
            ref isSolverDirty
        );

        DrawOption(
            "Durability",
            "Amount of weight to give to the craft's remaining durability.",
            config.ScoreDurabilityBonus,
            v => config = config with { ScoreDurabilityBonus = v },
            ref isSolverDirty
        );

        DrawOption(
            "CP",
            "Amount of weight to give to the craft's remaining CP.",
            config.ScoreCPBonus,
            v => config = config with { ScoreCPBonus = v },
            ref isSolverDirty
        );

        DrawOption(
            "Steps",
            "Amount of weight to give to the craft's number of steps. The lower\n" +
            "the step count, the higher the score.",
            config.ScoreFewerStepsBonus,
            v => config = config with { ScoreFewerStepsBonus = v },
            ref isSolverDirty
        );

        ImGuiUtils.EndGroupPanel();

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
