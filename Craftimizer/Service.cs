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
using Dalamud.Plugin.Services;

namespace Craftimizer.Plugin;

public sealed class Service
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; }
    [PluginService] public static ICommandManager CommandManager { get; private set; }
    [PluginService] public static IObjectTable Objects { get; private set; }
    [PluginService] public static ISigScanner SigScanner { get; private set; }
    [PluginService] public static IGameGui GameGui { get; private set; }
    [PluginService] public static IClientState ClientState { get; private set; }
    [PluginService] public static IDataManager DataManager { get; private set; }
    [PluginService] public static ITargetManager TargetManager { get; private set; }
    [PluginService] public static Condition Condition { get; private set; }
    [PluginService] public static Framework Framework { get; private set; }
    [PluginService] public static ITextureProvider TextureProvider { get; private set; }

    public static Plugin Plugin { get; internal set; }
    public static Configuration Configuration { get; internal set; }
    public static WindowSystem WindowSystem => Plugin.WindowSystem;
#pragma warning restore CS8618

}
