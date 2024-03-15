namespace Craftimizer.Simulator.Actions;

internal sealed class MuscleMemory : BaseAction
{
    public MuscleMemory()
    {
        Category = ActionCategory.FirstTurn;
        Level = 54;
        ActionId = 100379;
        IncreasesProgress = true;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 6;
    }

    public override void Efficiency(Simulator s, ref int eff)
    {
        eff = 300;
    }

    public override bool IsPossible(Simulator s) => s.IsFirstStep && base.IsPossible(s);

    public override bool CouldUse(Simulator s, ref int cost) => s.IsFirstStep && base.CouldUse(s, ref cost);

    public override void UseSuccess(Simulator s, ref int eff)
    {
        base.UseSuccess(s, ref eff);
        s.AddEffect(EffectType.MuscleMemory, 5);
    }
}
