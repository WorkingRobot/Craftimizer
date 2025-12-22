using Craftimizer.Plugin;
using Dalamud.Plugin;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using DotNext.Reflection;
using DotNext.Collections.Generic;

namespace Craftimizer.Utils;

public sealed class Ipc
{
    [AttributeUsage(AttributeTargets.Property)]
    private sealed class IPCCallAttribute(string? name) : Attribute
    {
        public string? Name { get; } = name;
    }

    public Ipc()
    {
        foreach (var prop in typeof(Ipc).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.GetCustomAttribute<IPCCallAttribute>() is not { } attr)
                continue;

            if (prop.GetMethod is not { } getMethod)
                throw new InvalidOperationException("Property must have a getter");

            if (!getMethod.IsDefined<CompilerGeneratedAttribute>())
                throw new InvalidOperationException("Property must have an auto getter");

            var type = prop.PropertyType;

            if (!typeof(Delegate).IsAssignableFrom(type))
                throw new InvalidOperationException("Property type must be a delegate");

            if (type.GetMethod("Invoke") is not { } typeMethod)
                throw new InvalidOperationException("Delegate type has no Invoke");

            var returnsVoid = typeMethod.ReturnType == typeof(void);

            var propSubscriber = typeof(IDalamudPluginInterface).GetMethod("GetIpcSubscriber", typeMethod.GetParameters().Length + 1, [typeof(string)]);
            if (propSubscriber is null)
                throw new InvalidOperationException("GetIpcSubscriber method not found");

            var callGateSubscriber = propSubscriber.MakeGenericMethod([.. typeMethod.GetParameterTypes(), returnsVoid ? typeof(int) : typeMethod.ReturnType]).Invoke(Service.PluginInterface, [attr.Name ?? prop.Name]);

            if (callGateSubscriber is null)
                throw new InvalidOperationException("CallGateSubscriber is null");

            var invokeFunc = callGateSubscriber.GetType().GetMethod(returnsVoid ? "InvokeAction" : "InvokeFunc");
            if (invokeFunc is null)
                throw new InvalidOperationException("Subscriber Invoke method not found");

            prop.SetValue(this, Delegate.CreateDelegate(type, callGateSubscriber, invokeFunc));

            Log.Debug($"Bound {prop.Name} IPC to {type}");
        }
    }

    [IPCCall("MacroMate.IsAvailable")]
    public Func<bool> MacroMateIsAvailable { get; private set; } = null!;

    [IPCCall("MacroMate.CreateOrUpdateMacro")]
    public Func<string, string, string?, uint?, bool> MacroMateCreateMacro { get; private set; } = null!;

    [IPCCall("MacroMate.ValidateGroupPath")]
    public Func<string, (bool, string?)> MacroMateValidateGroupPath { get; private set; } = null!;
}
