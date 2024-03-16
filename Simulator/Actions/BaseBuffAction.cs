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
        float defaultSuccessRate = 1) :
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

    public override void UseSuccess(Simulator s) =>
        s.AddEffect(Effect, Duration);

    public override string GetTooltip(Simulator s, bool addUsability)
    {
        var builder = new StringBuilder(base.GetTooltip(s, addUsability));
        builder.AppendLine($"{Duration} Steps");
        return builder.ToString();
    }

    protected string GetBaseTooltip(Simulator s, bool addUsability) =>
        base.GetTooltip(s, addUsability);
}
