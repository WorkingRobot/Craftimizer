namespace Craftimizer.Simulator.Actions;

internal class TricksOfTheTrade : BaseAction
{
    public TricksOfTheTrade(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 13;
    public override int ActionId => 100371;

    public override int CPCost => 0;
    public override int DurabilityCost => 0;
    public override bool IsGuaranteedAction => false;

    public override bool CanUse =>
        (Simulation.Condition == Condition.Good || Simulation.Condition == Condition.Excellent || Simulation.HasEffect(EffectType.HeartAndSoul))
        && base.CanUse;

    public override void UseSuccess()
    {
        Simulation.RestoreCP(20);
        if (Simulation.Condition != Condition.Good && Simulation.Condition != Condition.Excellent)
            Simulation.RemoveEffect(EffectType.HeartAndSoul);
    }
}
