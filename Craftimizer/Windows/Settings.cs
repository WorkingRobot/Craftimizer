using Craftimizer.Solver;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace Craftimizer.Plugin.Windows;

public class Settings : Window
{
    private static Configuration Config => Service.Configuration;

    private const int OptionWidth = 200;
    private static Vector2 OptionButtonSize => new(OptionWidth, ImGuiUtils.ButtonHeight);

    public const string TabGeneral = "General";
    public const string TabSimulator = "Simulator";
    public const string TabSynthHelper = "Synthesis Helper";
    public const string TabAbout = "About";

    private string? SelectedTab { get; set; }

    public Settings() : base("Craftimizer Settings")
    {
        Service.WindowSystem.AddWindow(this);

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Size = SizeConstraints.Value.MinimumSize;
        SizeCondition = ImGuiCond.Appearing;
    }

    public void SelectTab(string label)
    {
        SelectedTab = label;
    }

    private bool BeginTabItem(string label)
    {
        var isSelected = string.Equals(SelectedTab, label, StringComparison.Ordinal);
        if (isSelected)
        {
            SelectedTab = null;
            var open = true;
            return ImGui.BeginTabItem(label, ref open, ImGuiTabItemFlags.SetSelected);
        }
        return ImGui.BeginTabItem(label);
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
        ImGui.SetNextItemWidth(OptionWidth);
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
        ImGui.SetNextItemWidth(OptionWidth);
        if (ImGui.InputFloat(label, ref val))
        {
            setter(val);
            isDirty = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private static string GetAlgorithmName(SolverAlgorithm algorithm) =>
        algorithm switch
        {
            SolverAlgorithm.Oneshot => "Oneshot",
            SolverAlgorithm.OneshotForked => "Oneshot Forked",
            SolverAlgorithm.Stepwise => "Stepwise",
            SolverAlgorithm.StepwiseForked => "Stepwise Forked",
            SolverAlgorithm.StepwiseFurcated => "Stepwise Furcated",
            _ => "Unknown",
        };

    private static string GetAlgorithmTooltip(SolverAlgorithm algorithm) =>
        algorithm switch
        {
            SolverAlgorithm.Oneshot =>          "Run through all iterations and pick the best macro",
            SolverAlgorithm.OneshotForked =>    "Oneshot, but using multiple solvers simultaneously",
            SolverAlgorithm.Stepwise =>         "Run through all iterations and pick the next best step,\n" +
                                                "and repeat using previous steps as a starting point",
            SolverAlgorithm.StepwiseForked =>   "Stepwise, but using multiple solvers simultaneously",
            SolverAlgorithm.StepwiseFurcated => "Stepwise Forked, but the top N next best steps are\n" +
                                                "selected from the solvers, and each one is equally\n" +
                                                "used as a starting point",
            _ => "Unknown"
        };

    public override void Draw()
    {
        if (ImGui.BeginTabBar("settingsTabBar"))
        {
            DrawTabGeneral();
            DrawTabSimulator();
            if (Config.EnableSynthHelper)
                DrawTabSynthHelper();
            DrawTabAbout();

            ImGui.EndTabBar();
        }
    }

    private void DrawTabGeneral()
    {
        if (!BeginTabItem("General"))
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "Override Uncraftability Warning",
            "Allow simulation for crafts that otherwise wouldn't\n" +
            "be able to be crafted with your current gear",
            Config.OverrideUncraftability,
            v => Config.OverrideUncraftability = v,
            ref isDirty
        );

        DrawOption(
            "Enable Synthesis Helper",
            "Adds a helper next to your synthesis window to help solve for the best craft.\n" +
            "Extremely useful for expert recipes, where the condition can greatly affect\n" +
            "which actions you take.",
            Config.EnableSynthHelper,
            v => Config.EnableSynthHelper = v,
            ref isDirty
        );

        if (isDirty)
            Config.Save();

        ImGui.EndTabItem();
    }

    private static void DrawSolverConfig(ref SolverConfig configRef, SolverConfig defaultConfig, out bool isDirty)
    {
        isDirty = false;

        var config = configRef;

        ImGuiUtils.BeginGroupPanel("General");

        if (ImGui.Button("Reset to Default", OptionButtonSize))
        {
            config = defaultConfig;
            isDirty = true;
        }

        ImGui.SetNextItemWidth(OptionWidth);
        if (ImGui.BeginCombo("Algorithm", GetAlgorithmName(config.Algorithm)))
        {
            foreach (var alg in Enum.GetValues<SolverAlgorithm>())
            {
                if (ImGui.Selectable(GetAlgorithmName(alg), config.Algorithm == alg))
                {
                    config = config with { Algorithm = alg };
                    isDirty = true;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(GetAlgorithmTooltip(alg));
            }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "The algorithm to use when solving for a macro. Different\n" +
                "algorithms provide different pros and cons for using them.\n" +
                "By far, the Stepwise Furcated algorithm provides the best\n" +
                "results, especially for very difficult crafts."
            );

        DrawOption(
            "Iterations",
            "The total number of iterations to run per crafting step.\n" +
            "Higher values require more computational power. Higher values\n" +
            "also may decrease variance, so other values should be tweaked\n" +
            "as necessary to get a more favorable outcome.",
            config.Iterations,
            v => config = config with { Iterations = v },
            ref isDirty
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
            ref isDirty
        );

        DrawOption(
            "Exploration Constant",
            "A constant that decides how often the solver will explore new,\n" +
            "possibly good paths. If this value is too high,\n" +
            "moves will mostly be decided at random.",
            config.ExplorationConstant,
            v => config = config with { ExplorationConstant = v },
            ref isDirty
        );

        DrawOption(
            "Score Weighting Constant",
            "A constant ranging from 0 to 1 that configures how the solver\n" +
            "scores and picks paths to travel to next. A value of 0 means\n" +
            "actions will be chosen based on their average outcome, whereas\n" +
            "1 uses their best outcome achieved so far.",
            config.MaxScoreWeightingConstant,
            v => config = config with { MaxScoreWeightingConstant = v },
            ref isDirty
        );

        ImGui.BeginDisabled(config.Algorithm is not (SolverAlgorithm.OneshotForked or SolverAlgorithm.StepwiseForked or SolverAlgorithm.StepwiseFurcated));
        DrawOption(
            "Fork Count",
            "Split the number of iterations across different solvers. In general,\n" +
            "you should increase this value to at least the number of cores in\n" +
            $"your system (FYI, you have {Environment.ProcessorCount} cores) to attain the most speedup.\n" +
            "The higher the number, the more chance you have of finding a\n" +
            "better local maximum; this concept similar but not equivalent\n" +
            "to the exploration constant.\n" +
            "(Only used in the Forked and Furcated algorithms)",
            config.ForkCount,
            v => config = config with { ForkCount = v },
            ref isDirty
        );
        ImGui.EndDisabled();

        ImGui.BeginDisabled(config.Algorithm is not SolverAlgorithm.StepwiseFurcated);
        DrawOption(
            "Furcated Action Count",
            "On every craft step, pick this many top solutions and use them as\n" +
            "the input for the next craft step. For best results, use Fork Count / 2\n" +
            "and add about 1 or 2 more if needed.\n" +
            "(Only used in the Stepwise Furcated algorithm)",
            config.FurcatedActionCount,
            v => config = config with { FurcatedActionCount = v },
            ref isDirty
        );
        ImGui.EndDisabled();

        ImGuiUtils.EndGroupPanel();

        ImGuiUtils.BeginGroupPanel("Advanced");

        DrawOption(
            "Score Storage Threshold",
            "If a craft achieves this certain arbitrary score, the solver will\n" +
            "throw away all other possible combinations in favor of that one.\n" +
            "Only change this value if you absolutely know what you're doing.",
            config.ScoreStorageThreshold,
            v => config = config with { ScoreStorageThreshold = v },
            ref isDirty
        );

        DrawOption(
            "Max Rollout Step Count",
            "The maximum number of crafting steps every iteration can consider.\n" +
            "Decreasing this value can have unintended side effects. Only change\n" +
            "this value if you absolutely know what you're doing.",
            config.MaxRolloutStepCount,
            v => config = config with { MaxRolloutStepCount = v },
            ref isDirty
        );

        DrawOption(
            "Strict Actions",
            "When finding the next possible actions to execute, use a heuristic\n" +
            "to restrict which actions to attempt taking. This results in a much\n" +
            "better macro at the cost of not finding an extremely creative one.",
            config.StrictActions,
            v => config = config with { StrictActions = v },
            ref isDirty
        );

        ImGuiUtils.EndGroupPanel();

        ImGuiUtils.BeginGroupPanel("Score Weights (Advanced)");
        ImGui.TextWrapped("All values should add up to 1. Otherwise, the Score Storage Threshold should be changed.");
        ImGuiHelpers.ScaledDummy(10);

        DrawOption(
            "Progress",
            "Amount of weight to give to the craft's progress.",
            config.ScoreProgress,
            v => config = config with { ScoreProgress = v },
            ref isDirty
        );

        DrawOption(
            "Quality",
            "Amount of weight to give to the craft's quality.",
            config.ScoreQuality,
            v => config = config with { ScoreQuality = v },
            ref isDirty
        );

        DrawOption(
            "Durability",
            "Amount of weight to give to the craft's remaining durability.",
            config.ScoreDurability,
            v => config = config with { ScoreDurability = v },
            ref isDirty
        );

        DrawOption(
            "CP",
            "Amount of weight to give to the craft's remaining CP.",
            config.ScoreCP,
            v => config = config with { ScoreCP = v },
            ref isDirty
        );

        DrawOption(
            "Steps",
            "Amount of weight to give to the craft's number of steps. The lower\n" +
            "the step count, the higher the score.",
            config.ScoreSteps,
            v => config = config with { ScoreSteps = v },
            ref isDirty
        );

        if (ImGui.Button("Normalize Weights", OptionButtonSize))
        {
            var total = config.ScoreProgress +
                        config.ScoreQuality +
                        config.ScoreDurability +
                        config.ScoreCP +
                        config.ScoreSteps;
            config = config with
            {
                ScoreProgress = config.ScoreProgress / total,
                ScoreQuality = config.ScoreQuality / total,
                ScoreDurability = config.ScoreDurability / total,
                ScoreCP = config.ScoreCP / total,
                ScoreSteps = config.ScoreSteps / total
            };
            isDirty = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Normalize all weights to sum up to 1");

        ImGuiUtils.EndGroupPanel();

        if (isDirty)
            configRef = config;
    }

    private void DrawTabSimulator()
    {
        if (!BeginTabItem("Simulator"))
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "Show Only Learned Actions",
            "Don't show crafting actions that haven't been\n" +
            "learned yet with your current job on the simulator sidebar",
            Config.HideUnlearnedActions,
            v => Config.HideUnlearnedActions = v,
            ref isDirty
        );

        DrawOption(
            "Condition Randomness",
            "Allows the simulator condition to fluctuate randomly like a real craft.\n" +
            "Turns off when generating a macro.",
            Config.ConditionRandomness,
            v => Config.ConditionRandomness = v,
            ref isDirty
        );

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        var solverConfig = Config.SimulatorSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.SimulatorDefault, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.SimulatorSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();

        ImGui.EndTabItem();
    }

    private void DrawTabSynthHelper()
    {
        if (!BeginTabItem("Synthesis Helper"))
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "Step Count",
            "The number of future actions to solve for during an in-game craft.",
            Config.SynthHelperStepCount,
            v => Config.SynthHelperStepCount = v,
            ref isDirty
        );

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        var solverConfig = Config.SynthHelperSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.SynthHelperDefault, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.SynthHelperSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();

        ImGui.EndTabItem();
    }

    private void DrawTabAbout()
    {
        if (!BeginTabItem("About"))
            return;

        ImGuiHelpers.ScaledDummy(5);

        var plugin = Service.Plugin;
        var icon = plugin.Icon;

        ImGui.BeginTable("settingsAboutTable", 2);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, icon.Width);

        ImGui.TableNextColumn();
        ImGui.Image(icon.ImGuiHandle, new(icon.Width, icon.Height));

        ImGui.TableNextColumn();
        ImGui.Text($"{plugin.Name} v{plugin.Version} {plugin.BuildConfiguration}");
        ImGui.Text($"By {plugin.Author} (");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("WorkingRobot", "https://github.com/WorkingRobot");
        ImGui.SameLine(0, 0);
        ImGui.Text(")");

        ImGui.EndTable();

        ImGui.Text("Credit to altosock's ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("Craftingway", "https://craftingway.app");
        ImGui.SameLine(0, 0);
        ImGui.Text(" for the original solver algorithm");

        ImGui.EndTabItem();
    }
}
