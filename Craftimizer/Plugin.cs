using Craftimizer.Plugin.Windows;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using ClassJob = Craftimizer.Simulator.ClassJob;

namespace Craftimizer.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Craftimizer";

    public WindowSystem WindowSystem { get; }
    public SettingsWindow SettingsWindow { get; }
    public CraftingLog RecipeNoteWindow { get; }
    public SimulatorWindow? SimulatorWindow { get; set; }

    public Plugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        Service.Plugin = this;
        pluginInterface.Create<Service>();
        Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        WindowSystem = new(Name);

        RecipeNoteWindow = new();
        SettingsWindow = new();

        Service.CommandManager.AddHandler("/craft", new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += () => SettingsWindow.IsOpen = true;
    }

    public void OpenSimulatorWindow(Item item, bool isExpert, SimulationInput input, ClassJob classJob, List<ActionType> actions)
    {
        if (SimulatorWindow != null)
        {
            SimulatorWindow.IsOpen = false;
            WindowSystem.RemoveWindow(SimulatorWindow);
        }
        SimulatorWindow = new(item, isExpert, input, classJob, actions);
    }

    public void Dispose()
    {
        Service.CommandManager.RemoveHandler("/craft");
    }

    private void OnCommand(string command, string args)
    {
        if (command != "/craft")
            return;

        SettingsWindow.IsOpen = true;
    }
}
