namespace Craftimizer.Simulator.Actions;

internal sealed class HeartAndSoul : BaseBuffAction
{
    public HeartAndSoul()
    {
        Level = 86;
        Effect = EffectType.HeartAndSoul;
        MacroWaitTime = 3;
        ActionId = 100419;
        Category = ActionCategory.Other;
        IncreasesStepCount = false;
    }

    public override void CPCost(Simulator s, ref int cost)
    {
        cost = 0;
    }

    public override bool IsPossible(Simulator s) =>
        base.IsPossible(s) && s.Input.Stats.IsSpecialist && !s.ActionStates.UsedHeartAndSoul;

    public override bool CouldUse(Simulator s, ref int cost) => !s.ActionStates.UsedHeartAndSoul;

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{GetBaseTooltip(s, addUsability)}Specialist Only\n";
}
