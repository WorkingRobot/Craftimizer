namespace Craftimizer.Simulator.Actions;

internal class MastersMend : BaseAction
{
    public MastersMend(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 7;
    public override int ActionId => 100003;

    public override int CPCost => 88;
    public override int DurabilityCost => 0;

    public override void UseSuccess() =>
        Simulation.RestoreDurability(30);
}