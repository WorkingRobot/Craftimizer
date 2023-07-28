namespace Craftimizer.Simulator;

public sealed class SimulationInput
{
    public CharacterStats Stats { get; }
    public RecipeInfo Recipe { get; }
    public Random Random { get; }

    public int StartingQuality { get; }
    public int BaseProgressGain { get; }
    public int BaseQualityGain { get; }

    public SimulationInput(CharacterStats stats, RecipeInfo recipe, int startingQuality = 0)
    {
        Stats = stats;
        Recipe = recipe;
        Random = new();
        StartingQuality = startingQuality;

        // https://github.com/NotRanged/NotRanged.github.io/blob/0f4aee074f969fb05aad34feaba605057c08ffd1/app/js/ffxivcraftmodel.js#L88
        {
            var baseIncrease = (Stats.Craftsmanship * 10f / Recipe.ProgressDivider) + 2;
            if (Stats.CLvl <= Recipe.RLvl)
                baseIncrease *= Recipe.ProgressModifier / 100f;
            BaseProgressGain = (int)baseIncrease;
        }
        {
            var baseIncrease = (Stats.Control * 10f / Recipe.QualityDivider) + 35;
            if (Stats.CLvl <= Recipe.RLvl)
                baseIncrease *= Recipe.QualityModifier / 100f;
            BaseQualityGain = (int)baseIncrease;
        }
    }

    public Condition[] AvailableConditions => ConditionUtils.GetPossibleConditions(Recipe.ConditionsFlag);

    public override string ToString() =>
        $"SimulationInput {{ Stats = {Stats}, Recipe = {Recipe}, StartingQuality = {StartingQuality}, BaseProgressGain = {BaseProgressGain}, BaseQualityGain = {BaseQualityGain} }}";
}
