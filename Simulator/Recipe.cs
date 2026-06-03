namespace Craftimizer.Simulator;

public sealed record RecipeInfo
{
    public bool IsExpert { get; init; }
    public int ClassJobLevel { get; init; }
    public ushort ConditionsFlag { get; init; }
    public int MaxDurability { get; init; }
    public int MaxQuality { get; init; }
    public int MaxProgress { get; init; }

    // Quality needed to reach the recipe's highest collectability threshold.
    public int? CollectableTargetQuality { get; init; }

    public int QualityModifier { get; init; }
    public int QualityDivider { get; init; }
    public int ProgressModifier { get; init; }
    public int ProgressDivider { get; init; }
}
