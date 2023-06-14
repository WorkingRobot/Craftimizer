namespace Craftimizer.Simulator.Actions;

internal class Manipulation : BaseBuffAction
{
    public Manipulation(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 65;
    public override int ActionId => 4574;

    public override int CPCost => 96;

    public override Effect Effect => Effect.Manipulation;
    public override int EffectDuration => 8;
}
