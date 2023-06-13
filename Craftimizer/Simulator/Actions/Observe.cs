namespace Craftimizer.Simulator.Actions;

internal class Observe : BaseAction
{
    public Observe(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 13;

    public override int CPCost => 7;
    public override float Efficiency => 0f;
    public override int DurabilityCost => 0;

    public override void UseSuccess() { }
}
