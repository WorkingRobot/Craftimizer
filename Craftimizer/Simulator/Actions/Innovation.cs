namespace Craftimizer.Simulator.Actions;

internal class Innovation : BaseBuffAction
{
    public Innovation(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 26;
    public override int ActionId => 19004;

    public override int CPCost => 18;

    public override Effect Effect => new() { Type = EffectType.Innovation, Duration = 4 };
}
