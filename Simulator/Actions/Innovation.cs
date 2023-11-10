namespace Craftimizer.Simulator.Actions;

internal sealed class Innovation : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 26;
    public override uint ActionId => 19004;

    public override EffectType Effect => EffectType.Innovation;
    public override byte Duration => 4;

    public override int CPCost<S>(Simulator<S> s) => 18;
}
