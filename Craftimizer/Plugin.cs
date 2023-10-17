using Craftimizer.Plugin.Utils;
using Craftimizer.Plugin.Windows;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Craftimizer.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Craftimizer.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Version { get; }
    public string Author { get; }
    public string BuildConfiguration { get; }
    public IDalamudTextureWrap Icon { get; }

    public WindowSystem WindowSystem { get; }
    public Settings SettingsWindow { get; }
    public RecipeNote RecipeNoteWindow { get; }
    public MacroList ListWindow { get; private set; }
    public MacroEditor? EditorWindow { get; private set; }
    public MacroClipboard? ClipboardWindow { get; private set; }

    public Configuration Configuration { get; }
    public Hooks Hooks { get; }
    public IconManager IconManager { get; }

    public Plugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        Service.Initialize(this, pluginInterface);

        WindowSystem = new("Craftimizer");
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new();
        Hooks = new();
        IconManager = new();

        var assembly = Assembly.GetExecutingAssembly();
        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
        BuildConfiguration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        Icon = IconManager.GetAssemblyTexture("icon.png");

        SettingsWindow = new();
        RecipeNoteWindow = new();
        ListWindow = new();

        // Trigger static constructors so a huge hitch doesn't occur on first RecipeNote frame.
        FoodStatus.Initialize();
        Gearsets.Initialize();
        ActionUtils.Initialize();

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += OpenSettingsWindow;
        Service.PluginInterface.UiBuilder.OpenMainUi += OpenCraftingLog;

        Service.CommandManager.AddHandler("/craftimizer", new CommandInfo((_, _) => OpenSettingsWindow())
        {
            HelpMessage = "Open the settings window.",
        });
        Service.CommandManager.AddHandler("/craftmacros", new CommandInfo((_, _) => OpenMacroListWindow())
        {
            HelpMessage = "Open the crafting macros window.",
        });
        Service.CommandManager.AddHandler("/crafteditor", new CommandInfo((_, _) => OpenSettingsWindow())
        {
            HelpMessage = "Open the crafting macro editor.",
        });
    }

    public void OpenMacroEditor(CharacterStats characterStats, RecipeData recipeData, MacroEditor.CrafterBuffs buffs, IEnumerable<ActionType> actions, Action<IEnumerable<ActionType>>? setter)
    {
        EditorWindow?.Dispose();
        EditorWindow = new(characterStats, recipeData, buffs, actions, setter);
    }

    public void OpenSettingsWindow()
    {
        if (SettingsWindow.IsOpen ^= true)
            SettingsWindow.BringToFront();
    }

    public void OpenSettingsTab(string selectedTabLabel)
    {
        OpenSettingsWindow();
        SettingsWindow.SelectTab(selectedTabLabel);
    }

    public void OpenMacroListWindow()
    {
        ListWindow.IsOpen = true;
        ListWindow.BringToFront();
    }

    public void OpenCraftingLog()
    {
        Chat.SendMessage("/craftinglog");
    }

    public void OpenMacroClipboard(List<string> macros)
    {
        ClipboardWindow?.Dispose();
        ClipboardWindow = new(macros);
    }

    public void CopyMacro(IReadOnlyList<ActionType> actions) =>
        MacroCopy.Copy(actions);

    public void Dispose()
    {
        Service.CommandManager.RemoveHandler("/craftimizer");
        Service.CommandManager.RemoveHandler("/craftmacros");
        Service.CommandManager.RemoveHandler("/crafteditor");
        SettingsWindow.Dispose();
        RecipeNoteWindow.Dispose();
        ListWindow.Dispose();
        EditorWindow?.Dispose();
        ClipboardWindow?.Dispose();
        Hooks.Dispose();
        IconManager.Dispose();
    }
}
