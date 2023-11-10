namespace Craftimizer.Simulator.Actions;

internal sealed class GreatStrides : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 21;
    public override uint ActionId => 260;

    public override EffectType Effect => EffectType.GreatStrides;
    public override byte Duration => 3;

    public override int CPCost<S>(Simulator<S> s) => 32;
}
