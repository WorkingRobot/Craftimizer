namespace Craftimizer.Simulator.Actions;

internal sealed class Manipulation : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 65;
    public override uint ActionId => 4574;

    public override EffectType Effect => EffectType.Manipulation;
    public override byte Duration => 8;

    public override int CPCost(Simulator s) => 96;

    public override void Use(Simulator s)
    {
        if (s.HasEffect(EffectType.Manipulation))
            s.RestoreDurability(5);

        s.ReduceCP(CPCost(s));
        s.ReduceDurability(DurabilityCost);

        UseSuccess(s);

        s.IncreaseStepCount();
    }
}
