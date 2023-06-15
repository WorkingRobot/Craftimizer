namespace Craftimizer.Simulator.Actions;

internal class WasteNot2 : BaseBuffAction
{
    public WasteNot2(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 47;
    public override int ActionId => 4639;

    public override int CPCost => 98;

    public override Effect Effect => new() { Type = EffectType.WasteNot2, Duration = 8 };
    public override EffectType[] ConflictingEffects => new[] { EffectType.WasteNot };
}
