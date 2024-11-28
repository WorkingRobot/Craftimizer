using Craftimizer.Plugin.Windows;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Craftimizer.Windows;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
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
    public ILoadedTextureIcon Icon { get; }
    public const string SupportLink = "https://ko-fi.com/camora";

    public WindowSystem WindowSystem { get; }
    public Settings SettingsWindow { get; }
    public RecipeNote RecipeNoteWindow { get; }
    public SynthHelper SynthHelperWindow { get; }
    public MacroList ListWindow { get; private set; }
    public MacroEditor? EditorWindow { get; private set; }
    public MacroClipboard? ClipboardWindow { get; private set; }

    public Configuration Configuration { get; }
    public IconManager IconManager { get; }
    public Hooks Hooks { get; }
    public Chat Chat { get; }
    public CommunityMacros CommunityMacros { get; }
    public Ipc Ipc { get; }
    public AttributeCommandManager AttributeCommandManager { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Service.Initialize(this, pluginInterface);

        WindowSystem = new("Craftimizer");
        Configuration = Configuration.Load();
        IconManager = new();
        Hooks = new();
        Chat = new();
        CommunityMacros = new();
        Ipc = new();
        AttributeCommandManager = new();

        var assembly = Assembly.GetExecutingAssembly();
        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        Author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
        BuildConfiguration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        if (DateTime.Now is { Day: 1, Month: 4 })
            Icon = IconManager.GetAssemblyTexture("Graphics.horse_icon.png");
        else
            Icon = IconManager.GetAssemblyTexture("Graphics.icon.png");

        SettingsWindow = new();
        RecipeNoteWindow = new();
        SynthHelperWindow = new();
        ListWindow = new();

        // Trigger static constructors so a hitch doesn't occur on first RecipeNote frame.
        FoodStatus.Initialize();
        ActionUtils.Initialize();

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += OpenSettingsWindowForced;
        Service.PluginInterface.UiBuilder.OpenMainUi += OpenCraftingLog;
    }

    public (CharacterStats? Character, RecipeData? Recipe, MacroEditor.CrafterBuffs? Buffs) GetOpenedStats()
    {
        var editorWindow = (EditorWindow?.IsOpen ?? false) ? EditorWindow : null;
        var recipeData = editorWindow?.RecipeData ?? Service.Plugin.RecipeNoteWindow.RecipeData;
        var characterStats = editorWindow?.CharacterStats ?? Service.Plugin.RecipeNoteWindow.CharacterStats;
        var buffs = editorWindow?.Buffs ?? (RecipeNoteWindow.CharacterStats != null ? new(Service.ClientState.LocalPlayer?.StatusList) : null);

        return (characterStats, recipeData, buffs);
    }

    public (CharacterStats Character, RecipeData Recipe, MacroEditor.CrafterBuffs Buffs) GetDefaultStats()
    {
        var stats = GetOpenedStats();
        return (
            stats.Character ?? new()
            {
                Craftsmanship = 100,
                Control = 100,
                CP = 200,
                Level = 10,
                CanUseManipulation = false,
                HasSplendorousBuff = false,
                IsSpecialist = false,
            },
            stats.Recipe ?? new(1023),
            stats.Buffs ?? new(null)
        );
    }

    [Command(name: "/crafteditor", aliases: "/macroeditor", description: "Open the crafting macro editor.")]
    public void OpenEmptyMacroEditor()
    {
        var stats = GetDefaultStats();
        OpenMacroEditor(stats.Character, stats.Recipe, stats.Buffs, null, [], null);
    }

    public void OpenMacroEditor(CharacterStats characterStats, RecipeData recipeData, MacroEditor.CrafterBuffs buffs, IEnumerable<int>? ingredientHqCounts, IEnumerable<ActionType> actions, Action<IEnumerable<ActionType>>? setter)
    {
        EditorWindow?.Dispose();
        EditorWindow = new(characterStats, recipeData, buffs, ingredientHqCounts, actions, setter);
    }

    [Command(name: "/craftaction", description: "Execute the suggested action in the synthesis helper. Can also be run inside a macro. This command is useful for controller players.")]
    public void ExecuteSuggestedSynthHelperAction() =>
        SynthHelperWindow.ExecuteNextAction();

    [Command(name: "/craftretry", description: "Clicks \"Retry\" in the synthesis helper. Can also be run inside a macro. This command is useful for controller players.")]
    public void ExecuteRetrySynthHelper() =>
        SynthHelperWindow.AttemptRetry();

    [Command(name: "/craftimizer", description: "Open the settings window.")]
    private void OpenSettingsWindowForced() =>
        OpenSettingsWindow(true);

    public void OpenSettingsWindow(bool force = false)
    {
        if (SettingsWindow.IsOpen ^= !force || !SettingsWindow.IsOpen)
            SettingsWindow.BringToFront();
    }

    public void OpenSettingsTab(string selectedTabLabel)
    {
        OpenSettingsWindow(true);
        SettingsWindow.SelectTab(selectedTabLabel);
    }

    [Command(name: "/craftmacros", aliases: "/macrolist", description: "Open the crafting macros window.")]
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

    public IActiveNotification DisplaySolverWarning(string text) =>
        DisplayNotification(new()
        {
            Content = text,
            Title = "Solver Warning",
            Type = NotificationType.Warning
        });

    public IActiveNotification DisplayNotification(Notification notification)
    {
        var ret = Service.NotificationManager.AddNotification(notification);
        // ret.SetIconTexture(Icon.RentAsync().ContinueWith(t => (IDalamudTextureWrap?)t));
        return ret;
    }

    public void Dispose()
    {
        AttributeCommandManager.Dispose();
        SettingsWindow.Dispose();
        RecipeNoteWindow.Dispose();
        SynthHelperWindow.Dispose();
        ListWindow.Dispose();
        EditorWindow?.Dispose();
        ClipboardWindow?.Dispose();
        IconManager.Dispose();
        Hooks.Dispose();
        Icon.Dispose();
    }
}
