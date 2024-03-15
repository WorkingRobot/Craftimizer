using Craftimizer.Simulator.Actions;
using System.Collections.Frozen;

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
    private static readonly FrozenDictionary<ActionCategory, ActionType[]> SortedActions;

    static ActionCategoryUtils()
    {
        SortedActions =
            Enum.GetValues<ActionType>()
            .GroupBy(a => a.Category())
            .ToFrozenDictionary(g => g.Key, g => g.OrderBy(a => a.Level()).ToArray());
    }

    public static IReadOnlyList<ActionType> GetActions(this ActionCategory me)
    {
        if (SortedActions.TryGetValue(me, out var actions))
            return actions;

        throw new ArgumentException($"Unknown action category {me}", nameof(me));
    }

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
