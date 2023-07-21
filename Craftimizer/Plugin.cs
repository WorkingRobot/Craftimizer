using Craftimizer.Plugin.Windows;
using Craftimizer.Simulator;
using Craftimizer.Utils;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Craftimizer";

    public WindowSystem WindowSystem { get; }
    public Settings SettingsWindow { get; }
    public CraftingLog RecipeNoteWindow { get; }
    public Craft SynthesisWindow { get; }
    public Windows.Simulator? SimulatorWindow { get; set; }

    public Hooks Hooks { get; }
    public RecipeNote RecipeNote { get; }

    public Plugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        Service.Plugin = this;
        pluginInterface.Create<Service>();
        Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Hooks = new();
        RecipeNote = new();
        WindowSystem = new(Name);

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

    public void Dispose()
    {
        SimulatorWindow?.Dispose();
        SynthesisWindow.Dispose();
        RecipeNote.Dispose();
        Hooks.Dispose();
    }
}
