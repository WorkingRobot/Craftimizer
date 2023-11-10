namespace Craftimizer.Simulator.Actions;

internal sealed class CarefulObservation : BaseAction
{
    public override ActionCategory Category => ActionCategory.Other;
    public override int Level => 55;
    public override uint ActionId => 100395;
    public override int MacroWaitTime => 3;

    public override int DurabilityCost => 0;
    public override bool IncreasesStepCount => false;

    public override int CPCost<S>(Simulator<S> s) => 0;

    public override bool CanUse<S>(Simulator<S> s) => s.Input.Stats.IsSpecialist && s.ActionStates.CarefulObservationCount < 3;

    public override void UseSuccess<S>(Simulator<S> s) => s.StepCondition();

    public override string GetTooltip<S>(Simulator<S> s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}Specialist Only";
}
