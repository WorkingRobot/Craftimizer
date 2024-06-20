using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Craftimizer.Plugin.Windows;

public sealed class Settings : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    private static Configuration Config => Service.Configuration;

    private const int OptionWidth = 200;
    private static Vector2 OptionButtonSize => new(OptionWidth, ImGui.GetFrameHeight());

    private string? SelectedTab { get; set; }

    private IFontHandle HeaderFont { get; }
    private IFontHandle SubheaderFont { get; }

    public Settings() : base("Craftimizer Settings", WindowFlags)
    {
        Service.WindowSystem.AddWindow(this);

        HeaderFont = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx * 2f)));
        SubheaderFont = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx * 1.5f)));

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
            ImGuiUtils.TooltipWrapped(tooltip);
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
            ImGuiUtils.TooltipWrapped(tooltip);
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
                        ImGuiUtils.TooltipWrapped(getTooltip(type));
                }
            }
        }
        if (ImGui.IsItemHovered())
            ImGuiUtils.TooltipWrapped(tooltip);
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
            SolverAlgorithm.Stepwise =>         "Run through all iterations and pick the next best step, " +
                                                "and repeat using previous steps as a starting point",
            SolverAlgorithm.StepwiseForked =>   "Stepwise, but using multiple solvers simultaneously",
            SolverAlgorithm.StepwiseFurcated => "Stepwise Forked, but the top N next best steps are " +
                                                "selected from the solvers, and each one is equally " +
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
            MacroCopyConfiguration.CopyType.OpenWindow =>       "Open a dedicated window with all macros being copied. " +
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
            DrawTabRecipeNote();
            if (Config.EnableSynthHelper)
                DrawTabSynthHelper();
            DrawTabMacroEditor();
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
            "Adds a helper next to your synthesis window to help solve for the best craft. " +
            "Extremely useful for expert recipes, where the condition can greatly affect " +
            "which actions you take.",
            Config.EnableSynthHelper,
            v => Config.EnableSynthHelper = v,
            ref isDirty
        );

        DrawOption(
            "Show Only One Macro Stat in Crafting Log",
            "Only one stat will be shown for a macro. If a craft will be finished, quality " +
            "is shown. Otherwise, progress is shown. Durability and remaining CP will be " +
            "hidden.",
            Config.ShowOptimalMacroStat,
            v => Config.ShowOptimalMacroStat = v,
            ref isDirty
        );

        DrawOption(
            "Reliability Trial Count",
            "When testing for reliability of a macro in the editor, this many trials will be " +
            "run. You should set this value to at least 100 to get a reliable spread of data. " +
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
                    "Copy to the shared macros tab. Leaving this unchecked copies to the " +
                    "individual tab.",
                    Config.MacroCopy.SharedMacro,
                    v => Config.MacroCopy.SharedMacro = v,
                    ref isDirty
                );

                DrawOption(
                    "Macro Number",
                    "The # of the macro to being copying to. Subsequent macros will be " +
                    "copied relative to this macro.",
                    Config.MacroCopy.StartMacroIdx,
                    0, 99,
                    v => Config.MacroCopy.StartMacroIdx = v,
                    ref isDirty
                );

                DrawOption(
                    "Max Macro Copy Count",
                    "The maximum number of macros to be copied. Any more and a window is " +
                    "displayed with the rest of them.",
                    Config.MacroCopy.MaxMacroCount,
                    1, 99,
                    v => Config.MacroCopy.MaxMacroCount = v,
                    ref isDirty
                );
            }

            DrawOption(
                "Use Macro Chain's /nextmacro",
                "Replaces the last step with /nextmacro to run the next macro " +
                "automatically. Overrides Add End Notification except for the " +
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
                    ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                }
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("Macro Chain is not installed");
            }

            DrawOption(
                "Add Macro Lock",
                "Adds /mlock to the beginning of every macro. Prevents other " +
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
                        "Prioritize always having a notification sound at the end of " +
                        "every macro. Keeping this off prevents macros with only 1 action.",
                        Config.MacroCopy.ForceNotification,
                        v => Config.MacroCopy.ForceNotification = v,
                        ref isDirty
                    );
                }
                if (!isForceUseful && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGuiUtils.Tooltip("Only useful when Combine Macro is off");

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
                "The algorithm to use when solving for a macro. Different " +
                "algorithms provide different pros and cons for using them. " +
                "By far, the Stepwise Furcated algorithm provides the best " +
                "results, especially for very difficult crafts.",
                GetAlgorithmName,
                GetAlgorithmTooltip,
                config.Algorithm,
                v => config = config with { Algorithm = v },
                ref isDirty
            );

            DrawOption(
                "Iterations",
                "The total number of iterations to run per crafting step. " +
                "Higher values require more computational power. Higher values " +
                "also may decrease variance, so other values should be tweaked " +
                "as necessary to get a more favorable outcome.",
                config.Iterations,
                1000,
                1000000,
                v => config = config with { Iterations = v },
                ref isDirty
            );

            DrawOption(
                "Max Step Count",
                "The maximum number of crafting steps; this is generally the only " +
                "setting you should change, and it should be set to around 5 steps " +
                "more than what you'd expect. If this value is too low, the solver " +
                "won't learn much per iteration; too high and it will waste time " +
                "on useless extra steps.",
                config.MaxStepCount,
                1,
                100,
                v => config = config with { MaxStepCount = v },
                ref isDirty
            );

            DrawOption(
                "Exploration Constant",
                "A constant that decides how often the solver will explore new, " +
                "possibly good paths. If this value is too high, " +
                "moves will mostly be decided at random.",
                config.ExplorationConstant,
                0,
                10,
                v => config = config with { ExplorationConstant = v },
                ref isDirty
            );

            DrawOption(
                "Score Weighting Constant",
                "A constant ranging from 0 to 1 that configures how the solver " +
                "scores and picks paths to travel to next. A value of 0 means " +
                "actions will be chosen based on their average outcome, whereas " +
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
                    "The number of cores to use when solving. You should use as many " +
                    "as you can. If it's too high, it will have an effect on your gameplay " +
                    $"experience. A good estimate would be 1 or 2 cores less than your " +
                    $"system (FYI, you have {Environment.ProcessorCount} cores), but make sure to accomodate " +
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
                    "Split the number of iterations across different solvers. In general, " +
                    "you should increase this value to at least the number of cores in " +
                    $"your system (FYI, you have {Environment.ProcessorCount} cores) to attain the most speedup. " +
                    "The higher the number, the more chance you have of finding a " +
                    "better local maximum; this concept similar but not equivalent " +
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
                    "On every craft step, pick this many top solutions and use them as " +
                    "the input for the next craft step. For best results, use Fork Count / 2 " +
                    "and add about 1 or 2 more if needed.\n" +
                    "(Only used in the Stepwise Furcated algorithm)",
                    config.FurcatedActionCount,
                    1,
                    500,
                    v => config = config with { FurcatedActionCount = v },
                    ref isDirty
                );
        }

        using (var panel = ImRaii2.GroupPanel("Action Pool", -1, out var poolWidth))
        {
            poolWidth -= ImGui.GetStyle().ItemSpacing.X * 2;

            ImGui.TextUnformatted("Select the actions you want the solver to choose from.");

            var pool = config.ActionPool;
            DrawActionPool(ref pool, poolWidth, out var isPoolDirty);
            if (isPoolDirty)
            {
                config = config with { ActionPool = pool };
                isDirty = true;
            }
        }

        using (var panel = ImRaii2.GroupPanel("Advanced", -1, out _))
        {
            DrawOption(
                "Score Storage Threshold",
                "If a craft achieves this certain arbitrary score, the solver will " +
                "throw away all other possible combinations in favor of that one. " +
                "Only change this value if you absolutely know what you're doing.",
                config.ScoreStorageThreshold,
                0,
                1,
                v => config = config with { ScoreStorageThreshold = v },
                ref isDirty
            );

            DrawOption(
                "Max Rollout Step Count",
                "The maximum number of crafting steps every iteration can consider. " +
                "Decreasing this value can have unintended side effects. Only change " +
                "this value if you absolutely know what you're doing.",
                config.MaxRolloutStepCount,
                1,
                50,
                v => config = config with { MaxRolloutStepCount = v },
                ref isDirty
            );

            DrawOption(
                "Strict Actions",
                "When finding the next possible actions to execute, use a heuristic " +
                "to restrict which actions to attempt taking. This results in a much " +
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
                "Amount of weight to give to the craft's number of steps. The lower " +
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
                ImGuiUtils.Tooltip("Normalize all weights to sum up to 1");
        }

        if (isDirty)
            configRef = config;
    }

    private static void DrawActionPool(ref ActionType[] actionPool, float poolWidth, out bool isDirty)
    {
        isDirty = false;

        var recipeData = Service.Plugin.GetDefaultStats().Recipe;
        HashSet<ActionType> pool = new(actionPool);

        var imageSize = ImGui.GetFrameHeight() * 2;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;

        using var _color = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero);
        using var _color3 = ImRaii.PushColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        using var _color2 = ImRaii.PushColor(ImGuiCol.ButtonActive, Vector4.Zero);
        using var _alpha = ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, ImGui.GetStyle().DisabledAlpha * .5f);
        foreach (var category in Enum.GetValues<ActionCategory>())
        {
            if (category == ActionCategory.Combo)
                continue;

            var actions = category.GetActions();
            using var panel = ImRaii2.GroupPanel(category.GetDisplayName(), poolWidth, out var availSpace);
            var itemsPerRow = (int)MathF.Floor((availSpace + spacing) / (imageSize + spacing));
            var itemCount = actions.Count;
            var iterCount = (int)(Math.Ceiling((float)itemCount / itemsPerRow) * itemsPerRow);
            for (var i = 0; i < iterCount; i++)
            {
                if (i % itemsPerRow != 0)
                    ImGui.SameLine(0, spacing);
                if (i < itemCount)
                {
                    var actionBase = actions[i].Base();
                    var isEnabled = pool.Contains(actions[i]);
                    var isInefficient = SolverConfig.InefficientActions.Contains(actions[i]);
                    var isRisky = SolverConfig.RiskyActions.Contains(actions[i]);
                    var iconTint = Vector4.One;
                    if (!isEnabled)
                        iconTint = new(1, 1, 1, ImGui.GetStyle().DisabledAlpha);
                    else if (isInefficient)
                        iconTint = new(1, 1f, .5f, 1);
                    else if (isRisky)
                        iconTint = new(1, .5f, .5f, 1);
                    if (ImGui.ImageButton(actions[i].GetIcon(recipeData.ClassJob).ImGuiHandle, new(imageSize), default, Vector2.One, 0, default, iconTint))
                    {
                        isDirty = true;
                        if (isEnabled)
                            pool.Remove(actions[i]);
                        else
                            pool.Add(actions[i]);
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        var s = new StringBuilder();
                        s.AppendLine(actions[i].GetName(recipeData.ClassJob));
                        if (isInefficient)
                            s.AppendLine(
                                "Not recommended. This action may be randomly used in a " +
                                "detrimental way to the rest of the craft. Always use " +
                                "your best judgement if enabling this action.");
                        if (isRisky)
                            s.AppendLine(
                                "Useless; the solver currently doesn't take any risks in " +
                                "its crafts. It only takes steps that have a 100% chance of " +
                                "succeeding. If you want have a moment where you want to take " +
                                "risks in your craft (like in expert recipes), don't rely " +
                                "on the solver during that time.");
                        ImGuiUtils.TooltipWrapped(s.ToString());
                    }
                }
                else
                    ImGui.Dummy(new(imageSize));
            }
        }

        if (isDirty)
        {
            bool InPool(BaseComboAction action)
            {
                if (action.ActionTypeA.Base() is BaseComboAction { } aCombo)
                {
                    if (!InPool(aCombo))
                        return false;
                }
                else
                {
                    if (!pool.Contains(action.ActionTypeA))
                        return false;
                }
                if (action.ActionTypeB.Base() is BaseComboAction { } bCombo)
                {
                    if (!InPool(bCombo))
                        return false;
                }
                else
                {
                    if (!pool.Contains(action.ActionTypeB))
                        return false;
                }
                return true;
            }

            foreach(var combo in ActionCategory.Combo.GetActions())
            {
                if (combo.Base() is BaseComboAction { } comboAction)
                {
                    if (!InPool(comboAction))
                        pool.Remove(combo);
                    else
                        pool.Add(combo);
                }
            }
            actionPool = [.. pool];
        }
    }

    private void DrawTabRecipeNote()
    {
        using var tab = TabItem("Crafting Log");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawOption(
            "Pin Helper Window",
            "Pins the helper window to the right of your crafting log. Disabling this will " +
            "allow you to move it around.",
            Config.PinRecipeNoteToWindow,
            v => Config.PinRecipeNoteToWindow = v,
            ref isDirty
        );

        DrawOption(
            "Automatically Suggest Macro",
            "(Can cause frame drops!) When navigating to a new recipe or changing your gear " +
            "stats, automatically suggest a new macro (equivalent to clicking \"Generate\" " +
            "in the Macro Editor). This can cause harsh frame drops on some computers or " +
            "recipes when underleveled while navigating the crafting log. Turning this off " +
            "provides a button to allow you to manually suggest a macro only when you need it.",
            Config.SuggestMacroAutomatically,
            v => Config.SuggestMacroAutomatically = v,
            ref isDirty
        );

        DrawOption(
            "Enable Community Macros",
            "Use FFXIV Teamcraft's community rotations to search for and find the best possible " +
            "crowd-sourced macro for your craft. This sends a request to their servers to retrieve " +
            "a list of macros that apply to your craft's rlvl. Requests are only sent once per rlvl " +
            "and are always cached to reduce server load.",
            Config.ShowCommunityMacros,
            v => Config.ShowCommunityMacros = v,
            ref isDirty
        );

        if (Config.ShowCommunityMacros)
        {
            DrawOption(
                "Automatically Search for Community Macro",
                "When navigating to a new recipe or changing your gear stats, automatically search " +
                "online for a new community macro.\n" +
                "This is turned off by default so you don't hammer their servers :)",
                Config.SearchCommunityMacroAutomatically,
                v => Config.SearchCommunityMacroAutomatically = v,
                ref isDirty
            );
        }

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        var solverConfig = Config.RecipeNoteSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.RecipeNoteDefault, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.RecipeNoteSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();
    }

    private void DrawTabMacroEditor()
    {
        using var tab = TabItem("Macro Editor");
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        var solverConfig = Config.EditorSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.EditorDefault, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.EditorSolverConfig = solverConfig;
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
            "Pin Helper Window",
            "Pins the synthesis helper to the right of your synthesis window. Disabling this will " +
            "allow you to move it around.",
            Config.PinSynthHelperToWindow,
            v => Config.PinSynthHelperToWindow = v,
            ref isDirty
        );

        DrawOption(
            "Disable When Running Macro",
            "Disables itself when an in-game macro is running.",
            Config.DisableSynthHelperOnMacro,
            v => Config.DisableSynthHelperOnMacro = v,
            ref isDirty
        );

        DrawOption(
            "Simulate Only First Step",
            "Only the first step is simulated by default. You can still " +
            "hover over the other steps to view their outcomes, but the " +
            "reliability trials (when hovering over the macro stats) are hidden.",
            Config.SynthHelperDisplayOnlyFirstStep,
            v => Config.SynthHelperDisplayOnlyFirstStep = v,
            ref isDirty
        );

        DrawOption(
            "Step Count",
            "The minimum number of future steps to solve for during an in-game craft.",
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
                ImGuiUtils.AlignMiddle(new(float.PositiveInfinity, HeaderFont.GetFontSize() + SubheaderFont.GetFontSize() + ImGui.GetFontSize() * 3 + ImGui.GetStyle().ItemSpacing.Y * 4), new(0, icon.Height));

                using (HeaderFont.Push())
                {
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize("Craftimizer").X);
                    ImGuiUtils.Hyperlink("Craftimizer", "https://github.com/WorkingRobot/craftimizer", false);
                }

                using (SubheaderFont.Push())
                    ImGuiUtils.TextCentered($"v{plugin.Version} {plugin.BuildConfiguration}");

                ImGuiUtils.AlignCentered(ImGui.CalcTextSize($"By {plugin.Author} (WorkingRobot)").X);
                ImGui.TextUnformatted($"By {plugin.Author} (");
                ImGui.SameLine(0, 0);
                ImGuiUtils.Hyperlink("WorkingRobot", "https://github.com/WorkingRobot");
                ImGui.SameLine(0, 0);
                ImGui.TextUnformatted(")");

                ImGuiUtils.AlignCentered(ImGui.CalcTextSize($"Ko-fi").X);
                ImGuiUtils.Hyperlink("Ko-fi", "https://ko-fi.com/camora");
            }
        }

        ImGuiHelpers.ScaledDummy(5);

        ImGui.Separator();

        ImGuiHelpers.ScaledDummy(5);

        using (SubheaderFont.Push())
            ImGuiUtils.TextCentered("Special Thanks");

        var startPosX = ImGui.GetCursorPosX();

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("alostsock", "https://github.com/alostsock");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" for making ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("Craftingway", "https://craftingway.app");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" and the original solver algorithm.");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("FFXIV Teamcraft", "https://ffxivteamcraft.com");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" and its users for their community rotations.");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this", "https://dke.maastrichtuniversity.nl/m.winands/documents/multithreadedMCTS2.pdf");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(", ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this", "https://liacs.leidenuniv.nl/~plaata1/papers/paper_ICAART18.pdf");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(", and ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this paper", "https://arxiv.org/abs/2308.04459");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" for inspiration and design references.");
    }

    public void Dispose()
    {
        Service.WindowSystem.RemoveWindow(this);
        SubheaderFont?.Dispose();
        HeaderFont?.Dispose();
    }
}
