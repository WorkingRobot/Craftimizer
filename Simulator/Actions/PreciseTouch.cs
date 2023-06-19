namespace Craftimizer.Simulator.Actions;

internal sealed class PreciseTouch : BaseAction
{
    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 53;
    public override uint ActionId => 100128;

    public override int CPCost => 18;
    public override float Efficiency => 1.50f;
    public override bool IncreasesQuality => true;
    public override bool IsGuaranteedAction => false;

    public override bool CanUse =>
        (Simulation.Condition == Condition.Good || Simulation.Condition == Condition.Excellent || Simulation.HasEffect(EffectType.HeartAndSoul))
        && base.CanUse;

    public override void UseSuccess()
    {
        base.UseSuccess();
        Simulation.StrengthenEffect(EffectType.InnerQuiet);
        if (Simulation.Condition != Condition.Good && Simulation.Condition != Condition.Excellent)
            Simulation.RemoveEffect(EffectType.HeartAndSoul);
    }
}
