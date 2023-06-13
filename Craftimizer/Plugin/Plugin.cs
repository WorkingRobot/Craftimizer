using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace Craftimizer.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Craftimizer";

    public Configuration Configuration { get; }
    public WindowSystem WindowSystem { get; } = new("Craftimizer");
    public SimulatorWindow SimulatorWindow { get; }

    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        SimulatorWindow = new SimulatorWindow();
        WindowSystem.AddWindow(SimulatorWindow);

        Service.CommandManager.AddHandler("/craft", new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += () => SimulatorWindow.IsOpen = true;

    }

    public void Dispose()
    {
        Service.CommandManager.RemoveHandler("/craft");
    }

    private void OnCommand(string command, string args)
    {
        if (command != "/craft")
            return;
    }
}
