namespace Craftimizer.Simulator.Actions;

internal class GreatStrides : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 21;
    public override uint ActionId => 260;

    public override int CPCost => 32;

    public override EffectType Effect => EffectType.GreatStrides;
    public override byte Duration => 3;
}
