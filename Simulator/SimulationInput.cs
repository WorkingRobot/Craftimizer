namespace Craftimizer.Simulator;

public sealed class SimulationInput
{
    public CharacterStats Stats { get; }
    public RecipeInfo Recipe { get; }
    public Random Random { get; }

    public int StartingQuality { get; }
    public int BaseProgressGain { get; }
    public int BaseQualityGain { get; }

    public SimulationInput(CharacterStats stats, RecipeInfo recipe, int startingQuality, Random random)
    {
        Stats = stats;
        Recipe = recipe;
        Random = random;
        StartingQuality = startingQuality;

        // https://github.com/NotRanged/NotRanged.github.io/blob/0f4aee074f969fb05aad34feaba605057c08ffd1/app/js/ffxivcraftmodel.js#L88
        {
            var baseIncrease = (Stats.Craftsmanship * 10f / Recipe.ProgressDivider) + 2;
            if (Stats.Level <= Recipe.ClassJobLevel)
                baseIncrease *= Recipe.ProgressModifier * 0.01f;
            BaseProgressGain = (int)baseIncrease;
        }
        {
            var baseIncrease = (Stats.Control * 10f / Recipe.QualityDivider) + 35;
            if (Stats.Level <= Recipe.ClassJobLevel)
                baseIncrease *= Recipe.QualityModifier * 0.01f;
            BaseQualityGain = (int)baseIncrease;
        }
    }

    public SimulationInput(CharacterStats stats, RecipeInfo recipe, int startingQuality = 0, int? seed = null) : this(stats, recipe, startingQuality, seed == null ? new Random() : new Random(seed.Value))
    {

    }

    public Condition[] AvailableConditions => ConditionUtils.GetPossibleConditions(Recipe.ConditionsFlag);

    public override string ToString() =>
        $"SimulationInput {{ Stats = {Stats}, Recipe = {Recipe}, Random = {Random}, StartingQuality = {StartingQuality}, BaseProgressGain = {BaseProgressGain}, BaseQualityGain = {BaseQualityGain} }}";
}
