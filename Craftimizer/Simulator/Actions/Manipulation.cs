namespace Craftimizer.Simulator.Actions;

internal class Manipulation : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Durability;
    public override int Level => 65;
    public override uint ActionId => 4574;

    public override int CPCost => 96;

    public override Effect Effect => new() { Type = EffectType.Manipulation, Duration = 8 };
}
