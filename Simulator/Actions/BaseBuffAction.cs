using System.Text;

namespace Craftimizer.Simulator.Actions;

internal abstract class BaseBuffAction : BaseAction
{
    public abstract EffectType Effect { get; }
    public virtual byte Duration => 1;

    public override int DurabilityCost => 0;

    public override void UseSuccess() =>
        Simulation.AddEffect(Effect, Duration);

    public override string GetTooltip(bool addUsability)
    {
        var builder = new StringBuilder(base.GetTooltip(addUsability));
        builder.AppendLine($"{Duration} Steps");
        return builder.ToString();
    }
}
