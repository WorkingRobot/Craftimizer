namespace Craftimizer.Simulator.Actions;

internal sealed class TrainedEye : BaseAction
{
    public TrainedEye()
    {
        Category = ActionCategory.FirstTurn;
        Level = 80;
        ActionId = 100283;
        IncreasesQuality = true;
    }

    public override void CPCost(Simulator s,ref int cost)
    {
        cost = 250;
    }

    public override bool IsPossible(Simulator s) => s.IsFirstStep &&
                                                    !s.Input.Recipe.IsExpert &&
                                                    s.Input.Stats.Level >= (s.Input.Recipe.ClassJobLevel + 10) &&
                                                    base.IsPossible(s);

    public override bool CouldUse(Simulator s, ref int cost) => s.IsFirstStep && base.CouldUse(s, ref cost);

    public override void UseSuccess(Simulator s, ref int eff) =>
        s.IncreaseQualityRaw(s.Input.Recipe.MaxQuality - s.Quality);

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{base.GetTooltip(s, addUsability)}+{s.Input.Recipe.MaxQuality - s.Quality} Quality";
}
