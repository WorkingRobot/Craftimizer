using Craftimizer.Plugin;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.Interface.ImGuiNotification;

namespace Craftimizer.Windows;

public sealed class MacroClipboard : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoCollapse;

    private List<string> Macros { get; }

    private readonly byte[] macroBuffer = [];

    public MacroClipboard(IEnumerable<string> macros) : base("Macro Clipboard", WindowFlags)
    {
        Macros = [.. macros];

        IsOpen = true;
        AllowPinning = false;
        AllowClickthrough = false;
        BringToFront();

        Service.WindowSystem.AddWindow(this);
    }

    public override void Draw()
    {
        var idx = 0;
        foreach(var macro in Macros)
            DrawMacro(idx++, macro);
    }

    private void DrawMacro(int idx, string macro)
    {
        using var id = ImRaii.PushId(idx);
        using var panel = ImRaii2.GroupPanel(Macros.Count == 1 ? "Macro" : $"Macro {idx + 1}", -1, out var availWidth);

        var cursor = ImGui.GetCursorPos();

        ImGuiUtils.AlignRight(ImGui.GetFrameHeight(), availWidth);
        var buttonCursor = ImGui.GetCursorPos();
        ImGui.InvisibleButton("##copyInvButton", new(ImGui.GetFrameHeight()));
        var buttonHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlapped | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var buttonActive = buttonHovered && ImGui.GetIO().MouseDown[(int)ImGuiMouseButton.Left];
        var buttonClicked = buttonHovered && ImGui.GetIO().MouseReleased[(int)ImGuiMouseButton.Left];
        ImGui.SetCursorPos(buttonCursor);
        {
            using var color = ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(buttonActive ? ImGuiCol.ButtonActive : ImGuiCol.ButtonHovered), buttonHovered);
            ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Paste);
            if (buttonClicked)
            {
                ImGui.SetClipboardText(macro);
                if (Service.Configuration.MacroCopy.ShowCopiedMessage)
                {
                    Service.Plugin.DisplayNotification(new()
                    {
                        Content = Macros.Count == 1 ? "Copied macro to clipboard." : $"Copied macro {idx + 1} to clipboard.",
                        MinimizedText = Macros.Count == 1 ? "Copied macro" : $"Copied macro {idx + 1}",
                        Title = "Macro Copied",
                        Type = NotificationType.Success
                    });
                }
            }
        }
        if (buttonHovered)
            ImGuiUtils.Tooltip("Copy to Clipboard");

        ImGui.SetCursorPos(cursor);
        {
            using var font = ImRaii.PushFont(UiBuilder.MonoFont);
            using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);
            using var bg = ImRaii.PushColor(ImGuiCol.FrameBg, Vector4.Zero);
            var lineCount = macro.Count(c => c == '\n') + 1;
            ImGui.InputTextMultiline("", macro, new(availWidth, ImGui.GetTextLineHeight() * Math.Max(15, lineCount) + ImGui.GetStyle().FramePadding.Y), ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AutoSelectAll);
        }

        if (buttonHovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Arrow);
    }

    public void Dispose()
    {
        Service.WindowSystem.RemoveWindow(this);
    }
}
