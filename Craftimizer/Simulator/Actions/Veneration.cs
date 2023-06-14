namespace Craftimizer.Simulator.Actions;

internal class Veneration : BaseAction
{
    public Veneration(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 15;
    public override int ActionId => 19297;

    public override int CPCost => 18;
    public override int DurabilityCost => 0;

    public override void UseSuccess() =>
        Simulation.AddEffect(Effect.Veneration, 4);
}
