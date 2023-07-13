namespace Craftimizer.Simulator;

public enum ActionCategory
{
    FirstTurn,
    Synthesis,
    Quality,
    Durability,
    Buffs,
    Combo,
    Other
}

public static class ActionCategoryUtils
{
    public static string GetDisplayName(this ActionCategory category) =>
        category switch
        {
            ActionCategory.FirstTurn => "First Turn",
            ActionCategory.Synthesis => "Synthesis",
            ActionCategory.Quality => "Quality",
            ActionCategory.Durability => "Durability",
            ActionCategory.Buffs => "Buffs",
            ActionCategory.Other => "Other",
            _ => category.ToString()
        };
}
