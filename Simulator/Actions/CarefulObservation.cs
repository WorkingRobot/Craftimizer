namespace Craftimizer.Simulator.Actions;

internal sealed class CarefulObservation : BaseAction
{
    public int CP = 0;

    public CarefulObservation()
    {
        Category = ActionCategory.Other;
        Level = 55;
        ActionId = 100395;
        MacroWaitTime = 3;
        DurabilityCost = 0;
        IncreasesStepCount = false;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = CP;
    }

    public override bool IsPossible(Simulator s) =>
        base.IsPossible(s) && s.Input.Stats.IsSpecialist && s.ActionStates.CarefulObservationCount < 3;

    public override bool CouldUse(Simulator s, ref int cost) => s.ActionStates.CarefulObservationCount < 3;

    public override void UseSuccess(Simulator s, ref int eff) => s.StepCondition();

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}Specialist Only\n";
}
