namespace Craftimizer.Simulator.Actions;

internal sealed class CarefulObservation : BaseAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 55;
    public override uint ActionId => 100395;
    public override int MacroWaitTime => 3;

    public override int DurabilityCost => 0;
    public override bool IncreasesStepCount => false;

    public override int CPCost(Simulator s) => 0;

    public override bool CanUse(Simulator s) => s.Input.Stats.IsSpecialist && s.ActionStates.CarefulObservationCount < 3;

    public override void UseSuccess(Simulator s) => s.StepCondition();

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}Specialist Only";
}
