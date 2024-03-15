namespace Craftimizer.Simulator.Actions;

internal sealed class ByregotsBlessing : BaseAction
{
    public int CP = 24;

    public ByregotsBlessing()
    {
        Category = ActionCategory.Quality;
        Level = 50;
        ActionId = 100339;
        IncreasesQuality = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 100 + (20 * s.GetEffectStrength(EffectType.InnerQuiet));
    }

    public override bool CouldUse(Simulator s, ref int cost) => s.HasEffect(EffectType.InnerQuiet) && base.CouldUse(s, ref cost);

    public override void UseSuccess(Simulator s, ref int eff)
    {
        base.UseSuccess(s, ref eff);
        s.RemoveEffect(EffectType.InnerQuiet);
    }
}
