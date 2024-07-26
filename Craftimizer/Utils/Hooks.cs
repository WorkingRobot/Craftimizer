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

    public delegate bool UseActionDelegate(ActionManager* manager, CSActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted);

    public readonly Hook<UseActionDelegate> UseActionHook = null!;

    public delegate byte IsActionHighlightedDelegate(ActionManager* manager, CSActionType actionType, uint actionId);

    public readonly Hook<IsActionHighlightedDelegate> IsActionHighlightedHook = null!;

    public Hooks()
    {
        UseActionHook = Service.GameInteropProvider.HookFromAddress<UseActionDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
        IsActionHighlightedHook = Service.GameInteropProvider.HookFromAddress<IsActionHighlightedDelegate>((nint)ActionManager.MemberFunctionPointers.IsActionHighlighted, IsActionHighlightedDetour);

        UseActionHook.Enable();
        IsActionHighlightedHook.Enable();
    }

    private bool UseActionDetour(ActionManager* manager, CSActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* optOutAreaTargeted)
    {
        var canCast = manager->GetActionStatus(actionType, actionId) == 0;
        var ret = UseActionHook.Original(manager, actionType, actionId, targetId, extraParam, mode, comboRouteId, optOutAreaTargeted);
        if (canCast && ret && actionType is CSActionType.CraftAction or CSActionType.Action)
        {
            var classJob = ClassJobUtils.GetClassJobFromIdx((byte)(Service.ClientState.LocalPlayer?.ClassJob.Id ?? 0));
            if (classJob != null)
            {
                var simActionType = ActionUtils.GetActionTypeFromId(actionId, classJob.Value, actionType == CSActionType.CraftAction);
                if (simActionType != null)
                {
                    try
                    {
                        OnActionUsed?.Invoke(simActionType.Value);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Failed to invoke OnActionUsed");
                    }
                }
            }
        }
        return ret;
    }

    private byte IsActionHighlightedDetour(ActionManager* manager, CSActionType actionType, uint actionId)
    {
        var ret = IsActionHighlightedHook.Original(manager, actionType, actionId);

        try
        {
            if (!Service.Configuration.SynthHelperAbilityAnts)
                return ret;

            if (!Service.Plugin.SynthHelperWindow.ShouldDrawAnts)
                return ret;

            if (actionType is not (CSActionType.CraftAction or CSActionType.Action))
                return ret;

            var jobId = Service.ClientState.LocalPlayer?.ClassJob.Id;
            if (jobId == null)
                return ret;

            var classJob = ClassJobUtils.GetClassJobFromIdx((byte)jobId.Value);
            if (classJob == null)
                return ret;

            var simActionType = ActionUtils.GetActionTypeFromId(actionId, classJob.Value, actionType == CSActionType.CraftAction);
            if (simActionType == null)
                return ret;

            if (Service.Plugin.SynthHelperWindow.NextAction != simActionType)
                return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check if action should be highlighted");
            return ret;
        }

        return 1;
    }

    public void Dispose()
    {
        UseActionHook.Dispose();
        IsActionHighlightedHook.Dispose();
    }
}
