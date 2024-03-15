using System.Text;

namespace Craftimizer.Simulator.Actions;

internal abstract class BaseBuffAction : BaseAction
{
    public BaseBuffAction()
    {
        MacroWaitTime = 2;
        DurabilityCost = 0;
    }

    // Non-instanced properties
    public EffectType Effect;
    public int Duration = 1;

    public override void UseSuccess(Simulator s, ref int eff) =>
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
