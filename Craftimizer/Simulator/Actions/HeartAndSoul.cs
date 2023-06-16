namespace Craftimizer.Simulator.Actions;

internal class HeartAndSoul : BaseBuffAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 86;
    public override uint ActionId => 100419;

    public override int CPCost => 0;
    public override bool IncreasesStepCount => false;

    public override Effect Effect => new() { Type = EffectType.HeartAndSoul };

    public override bool CanUse => Simulation.Stats.IsSpecialist && Simulation.CountPreviousAction(ActionType.HeartAndSoul) == 0;
}
