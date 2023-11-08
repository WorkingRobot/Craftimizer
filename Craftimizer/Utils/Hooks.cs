using Craftimizer.Plugin;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using ActionUtils = Craftimizer.Plugin.ActionUtils;
using CSActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace Craftimizer.Utils;

public sealed unsafe class Hooks : IDisposable
{
    public delegate void OnActionUsedDelegate(ActionType action);

    public event OnActionUsedDelegate? OnActionUsed;

    public delegate bool UseActionDelegate(ActionManager* manager, CSActionType actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, nint a8);

    public readonly Hook<UseActionDelegate> UseActionHook = null!;

    public Hooks()
    {
        UseActionHook = Service.GameInteropProvider.HookFromAddress<UseActionDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
        UseActionHook.Enable();
    }

    private bool UseActionDetour(ActionManager* manager, CSActionType actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, nint a8)
    {
        var canCast = manager->GetActionStatus(actionType, actionId) == 0;
        var ret = UseActionHook.Original(manager, actionType, actionId, targetId, param, useType, pvp, a8);
        if (canCast && ret && (actionType == CSActionType.CraftAction || actionType == CSActionType.Action))
        {
            var classJob = ClassJobUtils.GetClassJobFromIdx((byte)(Service.ClientState.LocalPlayer?.ClassJob.Id ?? 0));
            if (classJob != null)
            {
                var simActionType = ActionUtils.GetActionTypeFromId(actionId, classJob.Value, actionType == CSActionType.CraftAction);
                if (simActionType != null)
                    OnActionUsed?.Invoke(simActionType.Value);
            }
        }
        return ret;
    }

    public void Dispose()
    {
        UseActionHook.Dispose();
    }
}
