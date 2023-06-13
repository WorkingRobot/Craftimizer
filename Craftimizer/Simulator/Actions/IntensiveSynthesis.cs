namespace Craftimizer.Simulator.Actions;

internal class IntensiveSynthesis : BaseAction
{
    public IntensiveSynthesis(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 78;

    public override int CPCost => 6;
    public override float Efficiency => 4.00f;

    public override bool CanUse =>
        (Simulation.Condition == Condition.Good || Simulation.Condition == Condition.Excellent)
        && base.CanUse;

    public override void UseSuccess() =>
        Simulation.IncreaseProgress(Efficiency);
}
