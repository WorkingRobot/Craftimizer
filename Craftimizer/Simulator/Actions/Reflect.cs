namespace Craftimizer.Simulator.Actions;

internal class Reflect : BaseAction
{
    public Reflect(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 69;
    public override int ActionId => 100387;

    public override int CPCost => 6;
    public override float Efficiency => 1.00f;

    public override bool CanUse => Simulation.IsFirstStep && base.CanUse;

    public override void UseSuccess()
    {
        Simulation.IncreaseQuality(Efficiency);
        Simulation.StrengthenEffect(Effect.InnerQuiet);
    }
}
