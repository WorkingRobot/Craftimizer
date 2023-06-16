namespace Craftimizer.Simulator.Actions;

internal class WasteNot : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 15;
    public override uint ActionId => 4631;

    public override int CPCost => 56;

    public override Effect Effect => new() { Type = EffectType.WasteNot, Duration = 4 };
    public override EffectType[] ConflictingEffects => new[] { EffectType.WasteNot2 };
}
