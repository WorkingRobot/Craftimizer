namespace Craftimizer.Simulator.Actions;

internal class WasteNot2 : BaseAction
{
    public WasteNot2(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 47;
    public override int ActionId => 4639;

    public override int CPCost => 98;
    public override float Efficiency => 0f;
    public override int DurabilityCost => 0;

    public override void UseSuccess()
    {
        Simulation.RemoveEffect(Effect.WasteNot);
        Simulation.AddEffect(Effect.WasteNot2, 8);
    }
}
