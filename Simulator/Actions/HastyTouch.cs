namespace Craftimizer.Simulator.Actions;

internal sealed class HastyTouch() : BaseAction(
    ActionCategory.Quality, 9, 100355,
    increasesQuality: true,
    defaultCPCost: 0,
    defaultEfficiency: 100,
    defaultSuccessRate: 60
    )
{
    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);

        if (s.Input.Stats.Level >= 96)
            s.AddEffect(EffectType.Expedience, 1 + 1);
    }
}
