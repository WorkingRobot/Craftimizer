using Craftimizer.Plugin;
using Dalamud.Game.Command;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Craftimizer.Utils;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute(string name, string description, bool hidden = false, params string[] aliases) : Attribute
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool Hidden { get; } = hidden;
    public string[] Aliases { get; } = aliases;
}

public sealed class AttributeCommandManager : IDisposable
{
    private HashSet<string> RegisteredCommands { get; } = [];

    public AttributeCommandManager()
    {
        var target = Service.Plugin;
        foreach (var method in target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.GetCustomAttribute<CommandAttribute>() is not { } command)
                continue;

            var takesParams = method.GetParameters().Length != 0;

            CommandInfo.HandlerDelegate handler;
            if (takesParams)
                handler = method.CreateDelegate<CommandInfo.HandlerDelegate>(target);
            else
            {
                var invoker = method.CreateDelegate<Action>(target);
                handler = (_, _) => invoker();
            }

            var info = new CommandInfo(handler)
            {
                HelpMessage = command.Description,
                ShowInHelp = !command.Hidden,
            };

            var aliasInfo = new CommandInfo(handler)
            {
                HelpMessage = $"An alias for {command.Name}",
                ShowInHelp = !command.Hidden,
            };

            if (!RegisteredCommands.Add(command.Name))
                throw new InvalidOperationException($"Command '{command.Name}' is already registered.");

            if (!Service.CommandManager.AddHandler(command.Name, info))
                throw new InvalidOperationException($"Failed to register command '{command.Name}'.");

            foreach (var alias in command.Aliases)
            {
                if (!RegisteredCommands.Add(alias))
                    throw new InvalidOperationException($"Command '{alias}' is already registered.");

                if (!Service.CommandManager.AddHandler(alias, aliasInfo))
                    throw new InvalidOperationException($"Failed to register command '{alias}'.");
            }
        }
    }

    public void Dispose()
    {
        foreach (var command in RegisteredCommands)
            Service.CommandManager.RemoveHandler(command);
    }
}
