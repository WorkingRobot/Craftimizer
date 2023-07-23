using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace Craftimizer.Plugin;

public sealed class Service
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; }
    [PluginService] public static CommandManager CommandManager { get; private set; }
    [PluginService] public static ObjectTable Objects { get; private set; }
    [PluginService] public static SigScanner SigScanner { get; private set; }
    [PluginService] public static GameGui GameGui { get; private set; }
    [PluginService] public static ClientState ClientState { get; private set; }
    [PluginService] public static DataManager DataManager { get; private set; }
    [PluginService] public static TargetManager TargetManager { get; private set; }
    [PluginService] public static Condition Condition { get; private set; }
    [PluginService] public static Framework Framework { get; private set; }

    public static Plugin Plugin { get; internal set; }
    public static Configuration Configuration { get; internal set; }
    public static WindowSystem WindowSystem => Plugin.WindowSystem;
#pragma warning restore CS8618

}
