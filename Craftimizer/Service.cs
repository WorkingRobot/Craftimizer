using Dalamud.Data;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;

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
#pragma warning restore CS8618
}
