using Craftimizer.Plugin;
using System;

namespace Craftimizer.Utils;

public static class Log
{
    public static void Debug(string line) => Service.PluginLog.Debug(line);

    public static void Error(string line) => Service.PluginLog.Error(line);
    public static void Error(Exception e, string line) => Service.PluginLog.Error(e, line);
}
