namespace Craftimizer.Simulator.Actions;

internal sealed class QuickInnovation() : BaseBuffAction(
    ActionCategory.Other, 96, 100459,
    EffectType.Innovation, duration: 1,
    macroWaitTime: 3,
    increasesStepCount: false
    )
{
    public override bool IsPossible(Simulator s) =>
        base.IsPossible(s) && s.Input.Stats.IsSpecialist && !s.ActionStates.UsedQuickInnovation;

    public override bool CouldUse(Simulator s) =>
        !s.ActionStates.UsedQuickInnovation && !s.HasEffect(EffectType.Innovation);

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}Specialist Only\n";
}
