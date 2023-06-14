namespace Craftimizer.Simulator.Actions;

internal class Innovation : BaseBuffAction
{
    public Innovation(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 26;
    public override int ActionId => 19004;

    public override int CPCost => 18;

    public override Effect Effect => Effect.Innovation;
    public override int EffectDuration => 4;
}
