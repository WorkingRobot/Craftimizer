namespace Craftimizer.Simulator.Actions;

internal sealed class WasteNot2 : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 47;
    public override uint ActionId => 4639;

    public override int CPCost => 98;

    public override EffectType Effect => EffectType.WasteNot2;
    public override byte Duration => 8;
    public override EffectType[] ConflictingEffects => new[] { EffectType.WasteNot };
}
