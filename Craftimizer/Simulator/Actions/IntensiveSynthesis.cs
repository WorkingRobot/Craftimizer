namespace Craftimizer.Simulator.Actions;

internal class IntensiveSynthesis : BaseAction
{
    public IntensiveSynthesis(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 78;
    public override int ActionId => 100315;

    public override int CPCost => 6;
    public override float Efficiency => 4.00f;
    public override bool IncreasesProgress => true;
    public override bool IsGuaranteedAction => false;

    public override bool CanUse =>
        (Simulation.Condition == Condition.Good || Simulation.Condition == Condition.Excellent || Simulation.HasEffect(EffectType.HeartAndSoul))
        && base.CanUse;

    public override void UseSuccess()
    {
        base.UseSuccess();
        if (Simulation.Condition != Condition.Good && Simulation.Condition != Condition.Excellent)
            Simulation.RemoveEffect(EffectType.HeartAndSoul);
    }
}
