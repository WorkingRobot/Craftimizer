namespace Craftimizer.Simulator.Actions;

internal class GreatStrides : BaseBuffAction
{
    public GreatStrides(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 21;
    public override int ActionId => 260;

    public override int CPCost => 32;

    public override Effect Effect => Effect.GreatStrides;
    public override int EffectDuration => 3;
}
