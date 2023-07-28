using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public enum ActionHeuristicType : byte
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
    public static ActionSet AvailableActions(this ActionHeuristicType me, Simulator s) =>
        me switch
        {
            ActionHeuristicType.Normal => ActionHeuristicNormal.AvailableActions(s),
            ActionHeuristicType.Strict => ActionHeuristicStrict.AvailableActions(s),
            ActionHeuristicType.ExpertOpener => ActionHeuristicExpertOpener.AvailableActions(s),
            ActionHeuristicType.ExpertQuality => ActionHeuristicExpertQuality.AvailableActions(s),
            ActionHeuristicType.ExpertFinisher => ActionHeuristicExpertFinisher.AvailableActions(s),
            ActionHeuristicType.ExpertHydra => ActionHeuristicExpertHydra.AvailableActions(s),
            _ => ActionHeuristicNormal.AvailableActions(s)
        };
}
