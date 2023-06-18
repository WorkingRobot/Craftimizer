namespace Craftimizer.Simulator.Actions;

internal class Innovation : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 26;
    public override uint ActionId => 19004;

    public override int CPCost => 18;

    public override EffectType Effect => EffectType.Innovation;
    public override byte Duration => 4;
}
