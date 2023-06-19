namespace Craftimizer.Simulator.Actions;

internal sealed class Manipulation : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 65;
    public override uint ActionId => 4574;

    public override int CPCost => 96;

    public override EffectType Effect => EffectType.Manipulation;
    public override byte Duration => 8;

    public override void Use()
    {
        if (Simulation.HasEffect(EffectType.Manipulation))
            Simulation.RestoreDurability(5);

        Simulation.ReduceCP(CPCost);
        Simulation.ReduceDurability(DurabilityCost);

        UseSuccess();

        Simulation.IncreaseStepCount();
    }
}
