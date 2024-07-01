using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using System;
using System.Collections.Generic;

namespace Craftimizer.Utils;

public static class MacroCopy
{
    private const ClassJob DefaultJob = ClassJob.Carpenter;
    private const int MacroSize = 15;

    public static void Copy(IReadOnlyList<ActionType> actions)
    {
        if (actions.Count == 0)
        {
            Service.Plugin.DisplayNotification(new()
            {
                Content = "Cannot copy an empty macro.",
                MinimizedText = "Cannot copy empty macro",
                Title = "Macro Not Copied",
                Type = NotificationType.Error
            });
            return;
        }

        var config = Service.Configuration.MacroCopy;
        var macros = new List<string>();
        var s = new List<string>();
        for (var i = 0; i < actions.Count; ++i)
        {
            if (config.UseMacroLock && s.Count == 0)
            {
                s.Add("/mlock");
            }

            s.Add(GetActionCommand(actions[i], config));
            
            if (config.Type == MacroCopyConfiguration.CopyType.CopyToMacro || !config.CombineMacro)
            {
                if (i != actions.Count - 1 && (i != actions.Count - 2 || config.ForceNotification))
                {
                    if (s.Count == MacroSize - 1)
                    {
                        if (GetEndCommand(macros.Count, false, config) is { } endCommand)
                            s.Add(endCommand);
                    }
                    if (s.Count == MacroSize)
                    {
                        macros.Add(string.Join(Environment.NewLine, s));
                        s.Clear();
                    }
                }
            }
        }
        if (s.Count > 0)
        {
            if (s.Count < MacroSize || (config.Type != MacroCopyConfiguration.CopyType.CopyToMacro && config.CombineMacro))
            {
                if (GetEndCommand(macros.Count, true, config) is { } endCommand)
                    s.Add(endCommand);
            }
            macros.Add(string.Join(Environment.NewLine, s));
        }

        switch (config.Type)
        {
            case MacroCopyConfiguration.CopyType.OpenWindow:
                Service.Plugin.OpenMacroClipboard(macros);
                break;
            case MacroCopyConfiguration.CopyType.CopyToMacro:
                CopyToMacro(macros, config);
                break;
            case MacroCopyConfiguration.CopyType.CopyToClipboard:
                CopyToClipboard(macros);
                break;
        }
    }

    private static string GetActionCommand(ActionType action, MacroCopyConfiguration config)
    {
        var actionBase = action.Base();
        if (actionBase is BaseComboAction)
            throw new ArgumentException("Combo actions are not supported", nameof(action));
        if (config.Type != MacroCopyConfiguration.CopyType.CopyToMacro && config.RemoveWaitTimes)
            return $"/ac \"{action.GetName(DefaultJob)}\"";
        else
            return $"/ac \"{action.GetName(DefaultJob)}\" <wait.{actionBase.MacroWaitTime}>";
    }

    private static string? GetEndCommand(int macroIdx, bool isEnd, MacroCopyConfiguration config)
    {
        if (config.UseNextMacro && !isEnd)
        {
            if (config.Type == MacroCopyConfiguration.CopyType.CopyToMacro && config.CopyDown)
                return $"/nextmacro down";
            else
                return $"/nextmacro";
        }

        if (config.AddNotification)
        {
            if (isEnd)
            {
                if (config.AddNotificationSound)
                    return $"/echo Craft complete! <se.{config.EndNotificationSound}>";
                else
                    return $"/echo Craft complete!";
            }
            else
            {
                if (config.AddNotificationSound)
                    return $"/echo Macro #{macroIdx + 1} complete! <se.{config.IntermediateNotificationSound}>";
                else
                    return $"/echo Macro #{macroIdx + 1} complete!";
            }
        }
        return null;
    }

    private static void CopyToMacro(List<string> macros, MacroCopyConfiguration config)
    {
        int i, macroIdx;
        for (
            i = 0, macroIdx = config.StartMacroIdx;
            i < macros.Count && i < config.MaxMacroCount && macroIdx < 100;
            i++, macroIdx += config.CopyDown ? 10 : 1)
            SetMacro(macroIdx, config.SharedMacro, macros[i]);

        Service.Plugin.DisplayNotification(new()
        {
            Content = i > 1 ? "Copied macro to User Macros." : $"Copied {i} macros to User Macros.",
            MinimizedText = i > 1 ? "Copied macro" : $"Copied {i} macros",
            Title = "Macro Copied",
            Type = NotificationType.Success
        });
        if (i < macros.Count)
        {
            Service.Plugin.OpenMacroClipboard(macros);
            var rest = macros.Count - i;
            Service.Plugin.DisplayNotification(new()
            {
                Content = $"Couldn't copy {rest} macro{(rest == 1 ? "" : "s")}, so a window was opened with all of them.",
                Minimized = false,
                Title = "Macro Copied",
                Type = NotificationType.Warning
            });
        }
    }

    private static unsafe void SetMacro(int idx, bool isShared, string macroText)
    {
        if (idx >= 100 || idx < 0)
            throw new ArgumentOutOfRangeException(nameof(idx), "Macro index must be between 0 and 99");

        var module = RaptureMacroModule.Instance();
        var macro = module->GetMacro(isShared ? 1u : 0u, (uint)idx);
        var text = Utf8String.FromString(macroText.ReplaceLineEndings("\n"));
        module->ReplaceMacroLines(macro, text);
        text->Dtor();
        IMemorySpace.Free(text);
    }

    private static void CopyToClipboard(List<string> macros)
    {
        ImGui.SetClipboardText(string.Join(Environment.NewLine + Environment.NewLine, macros));
        Service.Plugin.DisplayNotification(new()
        {
            Content = macros.Count > 1 ? "Copied macro to clipboard." : $"Copied {macros.Count} macros to clipboard.",
            MinimizedText = macros.Count > 1 ? "Copied macro" : $"Copied {macros.Count} macros",
            Title = "Macro Copied",
            Type = NotificationType.Success
        });
    }
}
