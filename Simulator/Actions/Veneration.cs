namespace Craftimizer.Simulator.Actions;

internal class Veneration : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Buffs;
    public override int Level => 15;
    public override uint ActionId => 19297;

    public override int CPCost => 18;
    public override int DurabilityCost => 0;

    public override Effect Effect => new() { Type = EffectType.Veneration, Duration = 4 };
}
