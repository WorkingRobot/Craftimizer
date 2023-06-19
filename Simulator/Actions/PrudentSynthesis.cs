namespace Craftimizer.Simulator.Actions;

internal sealed class PrudentSynthesis : BaseAction
{
    public override ActionCategory Category => ActionCategory.Synthesis;
    public override int Level => 88;
    public override uint ActionId => 100427;

    public override int CPCost => 18;
    public override float Efficiency => 1.80f;
    public override bool IncreasesProgress => true;
    public override int DurabilityCost => base.DurabilityCost / 2;

    public override bool CanUse =>
        !(Simulation.HasEffect(EffectType.WasteNot) || Simulation.HasEffect(EffectType.WasteNot2))
        && base.CanUse;
}
