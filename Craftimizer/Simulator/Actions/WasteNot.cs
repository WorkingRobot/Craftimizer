namespace Craftimizer.Simulator.Actions;

internal class WasteNot : BaseAction
{
    public WasteNot(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 15;
    public override int ActionId => 4631;

    public override int CPCost => 56;
    public override int DurabilityCost => 0;

    public override void UseSuccess()
    {
        Simulation.RemoveEffect(Effect.WasteNot2);
        Simulation.AddEffect(Effect.WasteNot, 4);
    }
}
