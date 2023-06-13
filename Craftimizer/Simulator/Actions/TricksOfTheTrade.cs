namespace Craftimizer.Simulator.Actions;

internal class TricksOfTheTrade : BaseAction
{
    public TricksOfTheTrade(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 13;
    public override int ActionId => 100371;

    public override int CPCost => 0;
    public override float Efficiency => 0f;
    public override int DurabilityCost => 0;

    public override bool CanUse =>
        (Simulation.Condition == Condition.Good || Simulation.Condition == Condition.Excellent)
        && base.CanUse;

    public override void UseSuccess() =>
        Simulation.RestoreCP(20);
}
