namespace Craftimizer.Simulator.Actions;

internal sealed class CarefulObservation() : BaseAction(
    ActionCategory.Other, 55, 100395,
    durabilityCost: 0, increasesStepCount: false,
    defaultCPCost: 0
    )
{
    public override bool IsPossible(RotationSimulator s) =>
        base.IsPossible(s) && s.Input.Stats.IsSpecialist && s.ActionStates.CarefulObservationCount < 3;

    public override bool CouldUse(RotationSimulator s) =>
        s.ActionStates.CarefulObservationCount < 3;

    public override void UseSuccess(RotationSimulator s) =>
        s.StepCondition();

    public override string GetTooltip(RotationSimulator s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}Specialist Only\n";
}
