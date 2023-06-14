namespace Craftimizer.Simulator.Actions;

internal class Innovation : BaseAction
{
    public Innovation(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 26;
    public override int ActionId => 19004;

    public override int CPCost => 18;
    public override int DurabilityCost => 0;

    public override void UseSuccess() =>
        Simulation.AddEffect(Effect.Innovation, 4);
}
