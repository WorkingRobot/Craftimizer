namespace Craftimizer.Simulator.Actions;

internal sealed class CarefulObservation() : BaseAction(
    ActionCategory.Other, 55, 100395,
    durabilityCost: 0, increasesStepCount: false,
    defaultCPCost: 0
    )
{
    public override bool IsPossible(Simulator s) =>
        base.IsPossible(s) && s.Input.Stats.IsSpecialist && s.ActionStates.CarefulObservationCount < 3;

    public override bool CouldUse(Simulator s) =>
        s.ActionStates.CarefulObservationCount < 3;

    public override void UseSuccess(Simulator s) =>
        s.StepCondition();

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}Specialist Only\n";
}
