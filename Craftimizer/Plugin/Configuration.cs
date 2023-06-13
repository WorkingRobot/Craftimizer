using Dalamud.Configuration;
using System;

namespace Craftimizer.Plugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }
}
