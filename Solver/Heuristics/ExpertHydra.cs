using Craftimizer.Simulator;
using System.Diagnostics.Contracts;

namespace Craftimizer.Solver.Heuristics;

internal sealed class ExpertHydra // : IHeuristic
{
    [Pure]
    public static ActionSet AvailableActions(Simulator s)
    {
        var qualityTarget = s.Input.Recipe.MaxQuality * 0.8f; // 80% quality at least
        if (s.GetEffectStrength(EffectType.InnerQuiet) == 10 || s.Quality > qualityTarget)
            return ExpertFinisher.AvailableActions(s);

        qualityTarget = s.Input.Recipe.MaxQuality * 0.2f; // 20% quality at least
        var progressTarget = s.Input.Recipe.MaxProgress - s.Input.BaseProgressGain * 2.5f; // 250% efficiency away from completion
        if (s.Progress > progressTarget || s.Quality > qualityTarget)
            return ExpertQuality.AvailableActions(s);

        return ExpertOpener.AvailableActions(s);
    }
}
