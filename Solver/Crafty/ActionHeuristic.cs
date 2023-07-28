using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public enum ActionHeuristic : byte
{
    Normal,
    Strict,
    ExpertOpener,
    ExpertQuality,
    ExpertFinisher,
    ExpertHydra
}

public static class ActionHeuristicUtils
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ActionSet AvailableActions(this ActionHeuristic me, Simulator s) =>
        me switch
        {
            ActionHeuristic.Normal => ActionHeuristicNormal.AvailableActions(s),
            ActionHeuristic.Strict => ActionHeuristicStrict.AvailableActions(s),
            ActionHeuristic.ExpertOpener => ActionHeuristicExpertOpener.AvailableActions(s),
            ActionHeuristic.ExpertQuality => ActionHeuristicExpertQuality.AvailableActions(s),
            ActionHeuristic.ExpertFinisher => ActionHeuristicExpertFinisher.AvailableActions(s),
            ActionHeuristic.ExpertHydra => ActionHeuristicExpertHydra.AvailableActions(s),
            _ => ActionHeuristicNormal.AvailableActions(s)
        };
}
