using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Heuristics;

public enum HeuristicType : byte
{
    Normal,
    Strict,
    ExpertOpener,
    ExpertQuality,
    ExpertFinisher,
    ExpertHydra
}

public static class HeuristicUtils
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ActionSet AvailableActions(this HeuristicType me, Simulator s) =>
        me switch
        {
            HeuristicType.Normal => Normal.AvailableActions(s),
            HeuristicType.Strict => Strict.AvailableActions(s),
            HeuristicType.ExpertOpener => ExpertOpener.AvailableActions(s),
            HeuristicType.ExpertQuality => ExpertQuality.AvailableActions(s),
            HeuristicType.ExpertFinisher => ExpertFinisher.AvailableActions(s),
            HeuristicType.ExpertHydra => ExpertHydra.AvailableActions(s),
            _ => Normal.AvailableActions(s)
        };
}
