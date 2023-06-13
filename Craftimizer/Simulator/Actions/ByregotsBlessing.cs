namespace Craftimizer.Simulator.Actions;

internal class ByregotsBlessing : BaseAction
{
    public ByregotsBlessing(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Quality;
    public override int Level => 50;
    public override int ActionId => 100339;

    public override int CPCost => 24;
    public override float Efficiency => 1.00f + 0.20f * (Simulation.GetEffect(Effect.InnerQuiet)?.Strength ?? 0);

    public override bool CanUse => Simulation.HasEffect(Effect.InnerQuiet) && base.CanUse;

    public override void UseSuccess() =>
        Simulation.IncreaseQuality(Efficiency);
}
