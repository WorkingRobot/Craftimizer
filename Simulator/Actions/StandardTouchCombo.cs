namespace Craftimizer.Simulator.Actions;

// Basic Touch -> Standard Touch
internal sealed class StandardTouchCombo : BaseAction
{
    public override ActionCategory Category => ActionCategory.Combo;
    public override int Level => 18;
    public override uint ActionId => 100004;

    public override bool IncreasesQuality => true;

    public override int CPCost(Simulator s) => 18 + 18;

    public override bool CanUse(Simulator s) =>
        //           BasicTouch.DurabilityCost vv
        base.CanUse(s) && VerifyDurability2(s, 10);

    private static readonly BasicTouch ActionA = new();
    private static readonly StandardTouch ActionB = new();
    public override void Use(Simulator s)
    {
        s.ExecuteForced(ActionType.BasicTouch, ActionA);
        ActionB.Use(s);
    }

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n{ActionB.GetTooltip(s, addUsability)}";

    public static bool VerifyDurability2(Simulator s, int durabilityA)
    {
        var d = s.Durability;
        var wasteNots = s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2);

        // -A
        d -= (int)MathF.Ceiling(durabilityA * (wasteNots ? .5f : 1f));
        if (d <= 0)
            return false;

        // If we can do the first action and still have durability left to survive to the next
        // step (even before the Manipulation modifier), we can certainly do the next action.
        return true;
    }

    public static bool VerifyDurability3(Simulator s, int durabilityA, int durabilityB)
    {
        var d = s.Durability;
        var wasteNots = Math.Max(s.GetEffectDuration(EffectType.WasteNot), s.GetEffectDuration(EffectType.WasteNot2));
        var manips = s.GetEffectDuration(EffectType.Manipulation);

        d -= (int)MathF.Ceiling(durabilityA * wasteNots > 0 ? .5f : 1f);
        if (d <= 0)
            return false;

        if (manips > 0)
            d += 5;

        if (wasteNots > 0)
            wasteNots--;

        d -= (int)MathF.Ceiling(durabilityB * wasteNots > 0 ? .5f : 1f);

        if (d <= 0)
            return false;

        // If we can do the second action and still have durability left to survive to the next
        // step (even before the Manipulation modifier), we can certainly do the next action.
        return true;
    }
}
