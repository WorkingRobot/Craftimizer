namespace Craftimizer.Simulator.Actions;

internal sealed class Reflect : BaseAction
{
    public Reflect()
    {
        Category = ActionCategory.FirstTurn;
        Level = 69;
        ActionId = 100387;
        IncreasesQuality = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 6;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 100;
    }

    public override bool IsPossible(Simulator s) => s.IsFirstStep && base.IsPossible(s);

    public override bool CouldUse(Simulator s, ref int cost) => s.IsFirstStep && base.CouldUse(s, ref cost);

    public override void UseSuccess(Simulator s, ref int eff)
    {
        base.UseSuccess(s, ref eff);
        s.StrengthenEffect(EffectType.InnerQuiet);
    }
}
