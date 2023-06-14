namespace Craftimizer.Simulator.Actions;

internal class WasteNot : BaseBuffAction
{
    public WasteNot(Simulation simulation) : base(simulation) { }

    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 15;
    public override int ActionId => 4631;

    public override int CPCost => 56;

    public override Effect Effect => Effect.WasteNot;
    public override int EffectDuration => 4;
    public override Effect[] ConflictingEffects => new[] { Effect.WasteNot2 };
}
