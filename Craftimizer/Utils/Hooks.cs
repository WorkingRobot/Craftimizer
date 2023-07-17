using Craftimizer.Simulator;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using System;

namespace Craftimizer.Utils;

public sealed class Hooks : IDisposable
{
    public class ConditionUpdatedEventArgs : EventArgs
    {
        public Condition Condition { get; }

        public ConditionUpdatedEventArgs(Condition condition)
        {
            Condition = condition;
        }
    }

    public event EventHandler<ConditionUpdatedEventArgs>? OnConditionUpdated;

    public delegate void ActorControlSelfPrototype(uint entityId, uint type, uint a3, uint a4, uint a5, uint source, uint a7, uint a8, ulong a9, byte flag);

    // https://github.com/Kouzukii/ffxiv-deathrecap/blob/1298e75c5e15a6596e8678e85b8f1bde926051bf/Events/CombatEventCapture.cs#L82
    [Signature("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", DetourName = nameof(ActorControlSelfDetour))]
    public readonly Hook<ActorControlSelfPrototype> ActorControlSelfHook = null!;

    public Hooks()
    {
        SignatureHelper.Initialise(this);
        ActorControlSelfHook.Enable();
    }

    private bool HandleCondition(uint type, uint a3, uint a4)
    {
        // Crafting related or something?
        if (type != 300)
            return false;

        // Condition update
        if (a3 != 9)
            return false;

        // Invalid condition
        if (a4 < 2)
        {
            PluginLog.LogError($"Invalid condition {a4}");
            return false;
        }

        var condition = (Condition)(1 << ((int)a4 - 2));

        OnConditionUpdated?.Invoke(this, new(condition));
        return true;
    }

    private void ActorControlSelfDetour(uint entityId, uint type, uint a3, uint a4, uint a5, uint source, uint a7, uint a8, ulong a9, byte flag)
    {
        ActorControlSelfHook.Original(entityId, type, a3, a4, a5, source, a7, a8, a9, flag);

        if (HandleCondition(type, a3, a4))
            return;

        //PluginLog.LogDebug($"{entityId} {type} {a3} {a4} {a5} {a7} {a8} {a9} {flag}");
    }

    public void Dispose()
    {
        ActorControlSelfHook.Dispose();
    }
}
