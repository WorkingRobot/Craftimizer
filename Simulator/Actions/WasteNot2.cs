namespace Craftimizer.Simulator.Actions;

internal sealed class WasteNot2 : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 47;
    public override uint ActionId => 4639;

    public override EffectType Effect => EffectType.WasteNot2;
    public override byte Duration => 8;

    public override int CPCost<S>(Simulator<S> s) => 98;

    public override void UseSuccess<S>(Simulator<S> s)
    {
        base.UseSuccess(s);
        s.RemoveEffect(EffectType.WasteNot);
    }
}
