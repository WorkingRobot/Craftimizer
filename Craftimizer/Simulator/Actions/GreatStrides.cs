namespace Craftimizer.Simulator.Actions;

internal class GreatStrides : BaseAction
{
    public GreatStrides(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 21;
    public override int ActionId => 260;

    public override int CPCost => 32;
    public override float Efficiency => 0f;
    public override int DurabilityCost => 0;

    public override void UseSuccess() =>
        Simulation.AddEffect(Effect.GreatStrides, 3);
}
