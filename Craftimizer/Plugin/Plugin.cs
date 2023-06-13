using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace Craftimizer.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Craftimizer";

    public Configuration Configuration { get; }

    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Service.CommandManager.AddHandler("/craft", new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        //PluginInterface.UiBuilder.OpenConfigUi += () { };
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
