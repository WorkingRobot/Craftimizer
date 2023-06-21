namespace Craftimizer.Simulator.Actions;

internal sealed class Veneration : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 15;
    public override uint ActionId => 19297;

    public override EffectType Effect => EffectType.Veneration;
    public override byte Duration => 4;

    public override int CPCost(Simulator s) => 18;
}
