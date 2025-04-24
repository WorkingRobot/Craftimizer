using System.Text;

namespace Craftimizer.Simulator.Actions;

internal abstract class BaseBuffAction(
        ActionCategory category, int level, uint actionId,
        EffectType effect, int duration,
        int macroWaitTime = 2,
        bool increasesProgress = false, bool increasesQuality = false,
        int durabilityCost = 0, bool increasesStepCount = true,
        int defaultCPCost = 0,
        int defaultEfficiency = 0,
        int defaultSuccessRate = 100) :
    BaseAction(
        category, level, actionId,
        macroWaitTime,
        increasesProgress, increasesQuality,
        durabilityCost, increasesStepCount,
        defaultCPCost, defaultEfficiency, defaultSuccessRate)
{
    // Non-instanced properties
    public readonly EffectType Effect = effect;
    public readonly int Duration = duration;
    private readonly int trueDuration = increasesStepCount ? duration + 1 : duration;

    public override void UseSuccess(RotationSimulator s) =>
        s.AddEffect(Effect, trueDuration);

    public override string GetTooltip(RotationSimulator s, bool addUsability)
    {
        var builder = new StringBuilder(base.GetTooltip(s, addUsability));
        builder.AppendLine(Duration != 1 ? $"{Duration} Steps" : $"{Duration} Step");
        return builder.ToString();
    }

    protected string GetBaseTooltip(RotationSimulator s, bool addUsability) =>
        base.GetTooltip(s, addUsability);
}
