using Craftimizer.Solver;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace Craftimizer.Plugin.Windows;

public sealed class Settings : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    private static Configuration Config => Service.Configuration;

    private const int OptionWidth = 200;
    private static Vector2 OptionButtonSize => new(OptionWidth, ImGui.GetFrameHeight());

    private string? SelectedTab { get; set; }

    public Settings() : base("Craftimizer Settings", WindowFlags)
    {
        Service.WindowSystem.AddWindow(this);

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new(450, 400),
            MaximumSize = new(float.PositiveInfinity)
        };
    }

    public void SelectTab(string label)
    {
        SelectedTab = label;
    }

    private ImRaii.IEndObject TabItem(string label)
    {
        var isSelected = string.Equals(SelectedTab, label, StringComparison.Ordinal);
        if (isSelected)
        {
            SelectedTab = null;
            var open = true;
            return ImRaii.TabItem(label, ref open, ImGuiTabItemFlags.SetSelected);
        }
        return ImRaii.TabItem(label);
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

    private static void DrawOption<T>(string label, string tooltip, T value, T min, T max, Action<T> setter, ref bool isDirty) where T : struct, INumber<T>
    {
        ImGui.SetNextItemWidth(OptionWidth);
        var text = value.ToString();
        if (ImGui.InputText(label, ref text, 8, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal))
        {
            if (T.TryParse(text, null, out var newValue))
            {
                newValue = T.Clamp(newValue, min, max);
                if (value != newValue)
                {
                    setter(newValue);
                    isDirty = true;
                }
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private static void DrawOption<T>(string label, string tooltip, Func<T, string> getName, Func<T, string> getTooltip, T value, Action<T> setter, ref bool isDirty) where T : struct, Enum
    {
        ImGui.SetNextItemWidth(OptionWidth);
        using (var combo = ImRaii.Combo(label, getName(value)))
        {
            if (combo)
            {
                foreach (var type in Enum.GetValues<T>())
                {
                    if (ImGui.Selectable(getName(type), value.Equals(type)))
                    {
                        setter(type);
                        isDirty = true;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(getTooltip(type));
                }
            }
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

    private static string GetCopyTypeName(MacroCopyConfiguration.CopyType type) =>
        type switch
        {
            MacroCopyConfiguration.CopyType.OpenWindow => "Open a Window",
            MacroCopyConfiguration.CopyType.CopyToMacro => "Copy to Macros",
            MacroCopyConfiguration.CopyType.CopyToClipboard => "Copy to Clipboard",
            _ => "Unknown",
        };

    private static string GetCopyTypeTooltip(MacroCopyConfiguration.CopyType type) =>
        type switch
        {
            MacroCopyConfiguration.CopyType.OpenWindow =>       "Open a dedicated window with all macros being copied.\n" +
                                                                "Copy, view, and choose at your own leisure.",
            MacroCopyConfiguration.CopyType.CopyToMacro =>      "Copy directly to the game's macro system.",
            MacroCopyConfiguration.CopyType.CopyToClipboard =>  "Copy to your clipboard. Macros are separated by a blank line.",
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
        using var tab = TabItem("General");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "Enable Synthesis Helper",
            "Adds a helper next to your synthesis window to help solve for the best craft.\n" +
            "Extremely useful for expert recipes, where the condition can greatly affect\n" +
            "which actions you take.",
            Config.EnableSynthHelper,
            v => Config.EnableSynthHelper = v,
            ref isDirty
        );

        DrawOption(
            "Show Only One Macro Stat in Crafting Log",
            "Only one stat will be shown for a macro. If a craft will be finished, quality\n" +
            "is shown. Otherwise, progress is shown. Durability and remaining CP will be\n" +
            "hidden.",
            Config.ShowOptimalMacroStat,
            v => Config.ShowOptimalMacroStat = v,
            ref isDirty
        );

        DrawOption(
            "Reliability Trial Count",
            "When testing for reliability of a macro in the editor, this many trials will be\n" +
            "run. You should set this value to at least 100 to get a reliable spread of data.\n" +
            "If it's too low, you may not find an outlier, and the average might be skewed.",
            Config.ReliabilitySimulationCount,
            5,
            5000,
            v => Config.ReliabilitySimulationCount = v,
            ref isDirty
        );

        ImGuiHelpers.ScaledDummy(5);

        using (var panel = ImRaii2.GroupPanel("Copying Settings", -1, out _))
        {
            DrawOption(
                "Macro Copy Method",
                "The method to copy a macro with.",
                GetCopyTypeName,
                GetCopyTypeTooltip,
                Config.MacroCopy.Type,
                v => Config.MacroCopy.Type = v,
                ref isDirty
            );

            if (Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacro)
            {
                DrawOption(
                    "Copy Downwards",
                    "Copy subsequent macros downward (#1 -> #11) instead of to the right.",
                    Config.MacroCopy.CopyDown,
                    v => Config.MacroCopy.CopyDown = v,
                    ref isDirty
                );

                DrawOption(
                    "Copy to Shared Macros",
                    "Copy to the shared macros tab. Leaving this unchecked copies to the\n" +
                    "individual tab.",
                    Config.MacroCopy.SharedMacro,
                    v => Config.MacroCopy.SharedMacro = v,
                    ref isDirty
                );

                DrawOption(
                    "Macro Number",
                    "The # of the macro to being copying to. Subsequent macros will be\n" +
                    "copied relative to this macro.",
                    Config.MacroCopy.StartMacroIdx,
                    0, 99,
                    v => Config.MacroCopy.StartMacroIdx = v,
                    ref isDirty
                );

                DrawOption(
                    "Max Macro Copy Count",
                    "The maximum number of macros to be copied. Any more and a window is\n" +
                    "displayed with the rest of them.",
                    Config.MacroCopy.MaxMacroCount,
                    1, 99,
                    v => Config.MacroCopy.MaxMacroCount = v,
                    ref isDirty
                );
            }

            DrawOption(
                "Use Macro Chain's /nextmacro",
                "Replaces the last step with /nextmacro to run the next macro\n" +
                "automatically. Overrides Add End Notification except for the\n" +
                "last macro.",
                Config.MacroCopy.UseNextMacro,
                v => Config.MacroCopy.UseNextMacro = v,
                ref isDirty
            );

            if (Config.MacroCopy.UseNextMacro && !Service.PluginInterface.InstalledPlugins.Any(p => p.IsLoaded && string.Equals(p.InternalName, "MacroChain", StringComparison.Ordinal)))
            {
                ImGui.SameLine();
                using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                {
                    using var font = ImRaii.PushFont(UiBuilder.IconFont);
                    ImGui.Text(FontAwesomeIcon.ExclamationCircle.ToIconString());
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Macro Chain is not installed");
            }

            DrawOption(
                "Add Macro Lock",
                "Adds /mlock to the beginning of every macro. Prevents other\n" +
                "macros from being run.",
                Config.MacroCopy.UseMacroLock,
                v => Config.MacroCopy.UseMacroLock = v,
                ref isDirty
            );

            DrawOption(
                "Add Notification",
                "Replaces the last step of every macro with a /echo notification.",
                Config.MacroCopy.AddNotification,
                v => Config.MacroCopy.AddNotification = v,
                ref isDirty
            );

            if (Config.MacroCopy.AddNotification)
            {
                var isForceUseful = Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacro || !Config.MacroCopy.CombineMacro;
                using (var d = ImRaii.Disabled(!isForceUseful))
                {
                    DrawOption(
                        "Force Notification",
                        "Prioritize always having a notification sound at the end of\n" +
                        "every macro. Keeping this off prevents macros with only 1 action.",
                        Config.MacroCopy.ForceNotification,
                        v => Config.MacroCopy.ForceNotification = v,
                        ref isDirty
                    );
                }
                if (!isForceUseful && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Only useful when Combine Macro is off");

                DrawOption(
                    "Add Notification Sound",
                    "Adds a sound to the end of every macro.",
                    Config.MacroCopy.AddNotificationSound,
                    v => Config.MacroCopy.AddNotificationSound = v,
                    ref isDirty
                );

                if (Config.MacroCopy.AddNotificationSound)
                {
                    DrawOption(
                        "Intermediate Notification Sound",
                        "Ending notification sound for an intermediary macro.\n" +
                        "Uses <se.#>",
                        Config.MacroCopy.IntermediateNotificationSound,
                        1, 16,
                        v =>
                        {
                            Config.MacroCopy.IntermediateNotificationSound = v;
                            UIModule.PlayChatSoundEffect((uint)v);
                        },
                        ref isDirty
                    );

                    DrawOption(
                        "Final Notification Sound",
                        "Ending notification sound for the final macro.\n" +
                        "Uses <se.#>",
                        Config.MacroCopy.EndNotificationSound,
                        1, 16,
                        v =>
                        {
                            Config.MacroCopy.EndNotificationSound = v;
                            UIModule.PlayChatSoundEffect((uint)v);
                        },
                        ref isDirty
                    );
                }
            }

            if (Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacro)
            {
                DrawOption(
                    "Remove Wait Times",
                    "Remove <wait.#> at the end of every action. Useful for SomethingNeedDoing.",
                    Config.MacroCopy.RemoveWaitTimes,
                    v => Config.MacroCopy.RemoveWaitTimes = v,
                    ref isDirty
                );

                DrawOption(
                    "Combine Macro",
                    "Doesn't split the macro into smaller macros. Useful for SomethingNeedDoing.",
                    Config.MacroCopy.CombineMacro,
                    v => Config.MacroCopy.CombineMacro = v,
                    ref isDirty
                );
            }
        }

        if (isDirty)
            Config.Save();
    }

    private static void DrawSolverConfig(ref SolverConfig configRef, SolverConfig defaultConfig, out bool isDirty)
    {
        isDirty = false;

        var config = configRef;

        using (var panel = ImRaii2.GroupPanel("General", -1, out _))
        {
            if (ImGui.Button("Reset to Default", OptionButtonSize))
            {
                config = defaultConfig;
                isDirty = true;
            }

            DrawOption(
                "Algorithm",
                "The algorithm to use when solving for a macro. Different\n" +
                "algorithms provide different pros and cons for using them.\n" +
                "By far, the Stepwise Furcated algorithm provides the best\n" +
                "results, especially for very difficult crafts.",
                GetAlgorithmName,
                GetAlgorithmTooltip,
                config.Algorithm,
                v => config = config with { Algorithm = v },
                ref isDirty
            );

            DrawOption(
                "Iterations",
                "The total number of iterations to run per crafting step.\n" +
                "Higher values require more computational power. Higher values\n" +
                "also may decrease variance, so other values should be tweaked\n" +
                "as necessary to get a more favorable outcome.",
                config.Iterations,
                1000,
                500000,
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
                1,
                100,
                v => config = config with { MaxStepCount = v },
                ref isDirty
            );

            DrawOption(
                "Exploration Constant",
                "A constant that decides how often the solver will explore new,\n" +
                "possibly good paths. If this value is too high,\n" +
                "moves will mostly be decided at random.",
                config.ExplorationConstant,
                0,
                10,
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
                0,
                1,
                v => config = config with { MaxScoreWeightingConstant = v },
                ref isDirty
            );

            using (var d = ImRaii.Disabled(config.Algorithm is not (SolverAlgorithm.OneshotForked or SolverAlgorithm.StepwiseForked or SolverAlgorithm.StepwiseFurcated)))
                DrawOption(
                    "Max Core Count",
                    "The number of cores to use when solving. You should use as many\n" +
                    "as you can. If it's too high, it will have an effect on your gameplay\n" +
                    $"experience. A good estimate would be 1 or 2 cores less than your\n" +
                    $"system (FYI, you have {Environment.ProcessorCount} cores), but make sure to accomodate\n" +
                    $"for any other tasks you have in the background, if you have any.\n" +
                    "(Only used in the Forked and Furcated algorithms)",
                    config.MaxThreadCount,
                    1,
                    Environment.ProcessorCount,
                    v => config = config with { MaxThreadCount = v },
                    ref isDirty
                );

            using (var d = ImRaii.Disabled(config.Algorithm is not (SolverAlgorithm.OneshotForked or SolverAlgorithm.StepwiseForked or SolverAlgorithm.StepwiseFurcated)))
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
                    1,
                    500,
                    v => config = config with { ForkCount = v },
                    ref isDirty
                );

            using (var d = ImRaii.Disabled(config.Algorithm is not SolverAlgorithm.StepwiseFurcated))
                DrawOption(
                    "Furcated Action Count",
                    "On every craft step, pick this many top solutions and use them as\n" +
                    "the input for the next craft step. For best results, use Fork Count / 2\n" +
                    "and add about 1 or 2 more if needed.\n" +
                    "(Only used in the Stepwise Furcated algorithm)",
                    config.FurcatedActionCount,
                    1,
                    500,
                    v => config = config with { FurcatedActionCount = v },
                    ref isDirty
                );
        }

        using (var panel = ImRaii2.GroupPanel("Advanced", -1, out _))
        {
            DrawOption(
                "Score Storage Threshold",
                "If a craft achieves this certain arbitrary score, the solver will\n" +
                "throw away all other possible combinations in favor of that one.\n" +
                "Only change this value if you absolutely know what you're doing.",
                config.ScoreStorageThreshold,
                0,
                1,
                v => config = config with { ScoreStorageThreshold = v },
                ref isDirty
            );

            DrawOption(
                "Max Rollout Step Count",
                "The maximum number of crafting steps every iteration can consider.\n" +
                "Decreasing this value can have unintended side effects. Only change\n" +
                "this value if you absolutely know what you're doing.",
                config.MaxRolloutStepCount,
                1,
                50,
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
        }

        using (var panel = ImRaii2.GroupPanel("Score Weights (Advanced)", -1, out _))
        {
            ImGui.TextWrapped("All values should add up to 1. Otherwise, the Score Storage Threshold should be changed.");
            ImGuiHelpers.ScaledDummy(10);

            DrawOption(
                "Progress",
                "Amount of weight to give to the craft's progress.",
                config.ScoreProgress,
                0,
                1,
                v => config = config with { ScoreProgress = v },
                ref isDirty
            );

            DrawOption(
                "Quality",
                "Amount of weight to give to the craft's quality.",
                config.ScoreQuality,
                0,
                1,
                v => config = config with { ScoreQuality = v },
                ref isDirty
            );

            DrawOption(
                "Durability",
                "Amount of weight to give to the craft's remaining durability.",
                config.ScoreDurability,
                0,
                1,
                v => config = config with { ScoreDurability = v },
                ref isDirty
            );

            DrawOption(
                "CP",
                "Amount of weight to give to the craft's remaining CP.",
                config.ScoreCP,
                0,
                1,
                v => config = config with { ScoreCP = v },
                ref isDirty
            );

            DrawOption(
                "Steps",
                "Amount of weight to give to the craft's number of steps. The lower\n" +
                "the step count, the higher the score.",
                config.ScoreSteps,
                0,
                1,
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
        }

        if (isDirty)
            configRef = config;
    }

    private void DrawTabSimulator()
    {
        using var tab = TabItem("Simulator");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        var solverConfig = Config.SimulatorSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.SimulatorDefault, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.SimulatorSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();
    }

    private void DrawTabSynthHelper()
    {
        using var tab = TabItem("Synthesis Helper");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "Step Count",
            "The number of future actions to solve for during an in-game craft.",
            Config.SynthHelperStepCount,
            1,
            100,
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
    }

    private void DrawTabAbout()
    {
        using var tab = TabItem("About");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var plugin = Service.Plugin;
        var icon = plugin.Icon;

        using (var table = ImRaii.Table("settingsAboutTable", 2))
        {
            if (table)
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, icon.Width);

                ImGui.TableNextColumn();
                ImGui.Image(icon.ImGuiHandle, new(icon.Width, icon.Height));

                ImGui.TableNextColumn();
                ImGui.Text($"Craftimizer v{plugin.Version} {plugin.BuildConfiguration}");
                ImGui.Text($"By {plugin.Author} (");
                ImGui.SameLine(0, 0);
                ImGuiUtils.Hyperlink("WorkingRobot", "https://github.com/WorkingRobot");
                ImGui.SameLine(0, 0);
                ImGui.Text(")");
            }
        }

        ImGui.Text("Credit to altosock's ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("Craftingway", "https://craftingway.app");
        ImGui.SameLine(0, 0);
        ImGui.Text(" for the original solver algorithm");
    }

    public void Dispose()
    {
        Service.WindowSystem.RemoveWindow(this);
    }
}
