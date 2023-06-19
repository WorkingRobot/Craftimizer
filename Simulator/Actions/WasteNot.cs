namespace Craftimizer.Simulator.Actions;

internal sealed class WasteNot : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 15;
    public override uint ActionId => 4631;

    public override int CPCost => 56;

    public override EffectType Effect => EffectType.WasteNot;
    public override byte Duration => 4;
    public override EffectType[] ConflictingEffects => new[] { EffectType.WasteNot2 };
}
