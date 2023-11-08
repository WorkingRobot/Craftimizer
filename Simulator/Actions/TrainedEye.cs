namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedEye : BaseAction
{
    public override ActionCategory Category => ActionCategory.FirstTurn;
    public override int Level => 80;
    public override uint ActionId => 100283;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 250;

    public override bool CanUse(Simulator s) =>
        s.IsFirstStep &&
        !s.Input.Recipe.IsExpert &&
        s.Input.Stats.Level >= (s.Input.Recipe.ClassJobLevel + 10) &&
        base.CanUse(s);

    public override void UseSuccess(Simulator s) =>
        s.IncreaseQualityRaw(s.Input.Recipe.MaxQuality - s.Quality);

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}+{s.Input.Recipe.MaxQuality - s.Quality} Quality";
}
