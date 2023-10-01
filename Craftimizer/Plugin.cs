using Craftimizer.Plugin.Windows;
using Craftimizer.Simulator;
using Craftimizer.Utils;
using Craftimizer.Windows;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using System.Reflection;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Craftimizer";
    public string Version { get; }
    public string Author { get; }
    public string BuildConfiguration { get; }
    public TextureWrap Icon { get; }

    public WindowSystem WindowSystem { get; }
    public Settings SettingsWindow { get; }
    public Craftimizer.Windows.RecipeNote RecipeNoteWindow { get; }
    public Craft SynthesisWindow { get; }
    public Windows.Simulator? SimulatorWindow { get; set; }

    public Configuration Configuration { get; }
    public Hooks Hooks { get; }
    public Craftimizer.Utils.RecipeNote RecipeNote { get; }
    public IconManager IconManager { get; }

    public Plugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        Service.Initialize(this, pluginInterface);

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new();
        Hooks = new();
        RecipeNote = new();
        IconManager = new();
        WindowSystem = new(Name);

        var assembly = Assembly.GetExecutingAssembly();
        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
        BuildConfiguration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        Icon = IconManager.GetAssemblyTexture("icon.png");

        SettingsWindow = new();
        RecipeNoteWindow = new();
        SynthesisWindow = new();

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += OpenSettingsWindow;
    }

    public void OpenSimulatorWindow(Item item, bool isExpert, SimulationInput input, ClassJob classJob, Macro? macro)
    {
        if (SimulatorWindow != null)
        {
            SimulatorWindow.IsOpen = false;
            WindowSystem.RemoveWindow(SimulatorWindow);
        }
        SimulatorWindow = new(item, isExpert, input, classJob, macro);
    }

    public void OpenSettingsWindow()
    {
        SettingsWindow.IsOpen = true;
        SettingsWindow.BringToFront();
    }

    public void OpenSettingsTab(string selectedTabLabel)
    {
        OpenSettingsWindow();
        SettingsWindow.SelectTab(selectedTabLabel);
    }

    public void Dispose()
    {
        SimulatorWindow?.Dispose();
        SynthesisWindow.Dispose();
        RecipeNote.Dispose();
        Hooks.Dispose();
        IconManager.Dispose();
    }
}
