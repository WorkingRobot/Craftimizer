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

        var macros = GetMacros(actions, Service.Configuration.MacroCopy);

        switch (Service.Configuration.MacroCopy.Type)
        {
            case MacroCopyConfiguration.CopyType.OpenWindow:
                Service.Plugin.OpenMacroClipboard(macros);
                break;
            case MacroCopyConfiguration.CopyType.CopyToMacro:
                CopyToMacro(macros);
                break;
            case MacroCopyConfiguration.CopyType.CopyToClipboard:
                CopyToClipboard(macros);
                break;
            case MacroCopyConfiguration.CopyType.CopyToMacroMate:
                CopyToMacroMate(macros[0]);
                break;
        }
    }

    private static List<string> GetMacros(IReadOnlyList<ActionType> actions, MacroCopyConfiguration config)
    {
        var mustSplit = (config.Type == MacroCopyConfiguration.CopyType.CopyToMacro || !config.CombineMacro) && config.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate;

        var macros = new List<string>();
        
        var m = new List<string>();

        for (var i = 0; i < actions.Count; ++i)
        {
            var a = actions[i];
            var isLast = i == actions.Count - 1;
            var isSecondLast = i == actions.Count - 2;

            if (config.UseMacroLock && m.Count == 0)
                m.Add("/mlock");

            m.Add(GetActionCommand(a, config));

            if (mustSplit && !isLast)
            {
                var endLine = GetEndCommand(macros.Count, false, config);

                if (endLine != null && m.Count == MacroSize - 1)
                {
                    if (!isSecondLast || config.ForceNotification)
                        m.Add(endLine);
                }
            }

            if (mustSplit && m.Count == MacroSize)
            {
                macros.Add(string.Join(Environment.NewLine, m));
                m.Clear();
            }
        }

        if (m.Count != MacroSize && m.Count != 0)
        {
            if (GetEndCommand(macros.Count, true, config) is { } endLine)
                m.Add(endLine);
        }

        if (m.Count != 0)
            macros.Add(string.Join(Environment.NewLine, m));

        return macros;
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

    private static void CopyToMacro(List<string> macros)
    {
        var config = Service.Configuration.MacroCopy;

        int i, macroIdx;
        for (
            i = 0, macroIdx = config.StartMacroIdx;
            i < macros.Count && i < config.MaxMacroCount && macroIdx < 100;
            i++, macroIdx += config.CopyDown ? 10 : 1)
            SetMacro(macroIdx, config.SharedMacro, macros[i], i + 1);

        if (config.ShowCopiedMessage)
        {
            Service.Plugin.DisplayNotification(new()
            {
                Content = i > 1 ? "Copied macro to User Macros." : $"Copied {i} macros to User Macros.",
                MinimizedText = i > 1 ? "Copied macro" : $"Copied {i} macros",
                Title = "Macro Copied",
                Type = NotificationType.Success
            });
        }
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

    private static unsafe void SetMacro(int idx, bool isShared, string macroText, int macroIdx)
    {
        if (idx >= 100 || idx < 0)
            throw new ArgumentOutOfRangeException(nameof(idx), "Macro index must be between 0 and 99");

        var set = isShared ? 1u : 0u;

        var module = RaptureMacroModule.Instance();
        var macro = module->GetMacro(set, (uint)idx);
        if (!macro->IsNotEmpty())
        {
            macro->Name.SetString($"Craftimizer Macro {macroIdx}");
            macro->SetIcon((uint)(macroIdx > 10 ? 66161 : (66161 + macroIdx)));
        }
        var text = Utf8String.FromString(macroText.ReplaceLineEndings("\n"));
        module->ReplaceMacroLines(macro, text);
        text->Dtor();
        IMemorySpace.Free(text);

        RaptureHotbarModule.Instance()->ReloadMacroSlots((byte)set, (byte)idx);
    }

    private static void CopyToClipboard(List<string> macros)
    {
        ImGui.SetClipboardText(string.Join(Environment.NewLine + Environment.NewLine, macros));
        if (Service.Configuration.MacroCopy.ShowCopiedMessage)
        {
            Service.Plugin.DisplayNotification(new()
            {
                Content = macros.Count == 1 ? "Copied macro to clipboard." : $"Copied {macros.Count} macros to clipboard.",
                MinimizedText = macros.Count == 1 ? "Copied macro" : $"Copied {macros.Count} macros",
                Title = "Macro Copied",
                Type = NotificationType.Success
            });
        }
    }

    private static void CopyToMacroMate(string macro)
    {
        if (!Service.Ipc.MacroMateIsAvailable())
        {
            Service.Plugin.DisplayNotification(new()
            {
                Content = "Please check if it installed and enabled.",
                MinimizedText = "Macro Mate is unavailable",
                Title = "Macro Mate Unavailable",
                Type = NotificationType.Error
            });
            return;
        }

        var parentPath = Service.Configuration.MacroCopy.MacroMateParent;
        if (string.IsNullOrWhiteSpace(parentPath))
            parentPath = "/";

        var (isValidParent, parentError) = Service.Ipc.MacroMateValidateGroupPath(parentPath);
        if (!isValidParent)
        {
            Service.Plugin.DisplayNotification(new()
            {
                Content = parentError!,
                MinimizedText = parentError,
                Title = "Macro Mate Invalid Parent",
                Type = NotificationType.Error
            });
            return;
        }

        Service.Ipc.MacroMateCreateMacro(Service.Configuration.MacroCopy.MacroMateName, macro, parentPath, null);

        if (Service.Configuration.MacroCopy.ShowCopiedMessage)
        {
            Service.Plugin.DisplayNotification(new()
            {
                Content = "Copied macro to Macro Mate.",
                MinimizedText = "Copied macro",
                Title = "Macro Copied",
                Type = NotificationType.Success
            });
        }
    }
}
