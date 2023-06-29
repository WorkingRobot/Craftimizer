using System.Text;

namespace Craftimizer.Simulator.Actions;

internal abstract class BaseBuffAction : BaseAction
{
    // Non-instanced properties
    public abstract EffectType Effect { get; }
    public virtual byte Duration => 1;
    public override int MacroWaitTime => 2;

    public sealed override int DurabilityCost => 0;

    public override void UseSuccess(Simulator s) =>
        s.AddEffect(Effect, Duration);

    public sealed override string GetTooltip(Simulator s, bool addUsability)
    {
        var builder = new StringBuilder(base.GetTooltip(s, addUsability));
        builder.AppendLine($"{Duration} Steps");
        return builder.ToString();
    }
}
