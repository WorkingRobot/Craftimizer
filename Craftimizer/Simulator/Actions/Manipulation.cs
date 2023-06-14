namespace Craftimizer.Simulator.Actions;

internal class Manipulation : BaseAction
{
    public Manipulation(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 65;
    public override int ActionId => 4574;

    public override int CPCost => 96;
    public override int DurabilityCost => 0;

    public override void UseSuccess() =>
        Simulation.AddEffect(Effect.Manipulation, 8);
}
