namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedEye() : BaseAction(
    ActionCategory.FirstTurn, 80, 100283,
    increasesQuality: true,
    durabilityCost: 0,
    defaultCPCost: 250
    )
{
    public override bool IsPossible(RotationSimulator s) =>
        s.IsFirstStep &&
        !s.Input.Recipe.IsExpert &&
        s.Input.Stats.Level >= (s.Input.Recipe.ClassJobLevel + 10) &&
        base.IsPossible(s);

    public override bool CouldUse(RotationSimulator s) =>
        s.IsFirstStep && base.CouldUse(s);

    public override void UseSuccess(RotationSimulator s) =>
        s.IncreaseQualityRaw(s.Input.Recipe.MaxQuality - s.Quality);

    public override string GetTooltip(RotationSimulator s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}+{s.Input.Recipe.MaxQuality - s.Quality} Quality";
}
