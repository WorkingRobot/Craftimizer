namespace Craftimizer.Simulator.Actions;

public abstract class BaseComboAction(
        ActionType actionTypeA, ActionType actionTypeB,
        BaseAction actionA, BaseAction actionB,
        int? defaultCPCost = null) :
    BaseAction(
        ActionCategory.Combo, Math.Max(actionA.Level, actionA.Level), actionB.ActionId,
        increasesProgress: actionA.IncreasesProgress || actionB.IncreasesProgress,
        increasesQuality: actionA.IncreasesQuality || actionB.IncreasesQuality,
        defaultCPCost: defaultCPCost ?? (actionA.DefaultCPCost + actionB.DefaultCPCost))
{
    public readonly ActionType ActionTypeA = actionTypeA;
    public readonly ActionType ActionTypeB = actionTypeB;

    protected bool BaseCouldUse(Simulator s) =>
        base.CouldUse(s);

    private static bool VerifyDurability2(int durabilityA, int durability, in Effects effects)
    {
        if (!effects.HasEffect(EffectType.TrainedPerfection))
        {
            var wasteNots = effects.HasEffect(EffectType.WasteNot) || effects.HasEffect(EffectType.WasteNot2);
            // -A
            durability -= (int)MathF.Ceiling(durabilityA * (wasteNots ? .5f : 1f));
            if (durability <= 0)
                return false;
        }

        // If we can do the first action and still have durability left to survive to the next
        // step (even before the Manipulation modifier), we can certainly do the next action.
        return true;
    }

    public static bool VerifyDurability2(Simulator s, int durabilityA) =>
        VerifyDurability2(durabilityA, s.Durability, s.ActiveEffects);

    public static bool VerifyDurability3(int durabilityA, int durabilityB, int durability, in Effects effects)
    {
        var wasteNots = Math.Max(effects.GetDuration(EffectType.WasteNot), effects.GetDuration(EffectType.WasteNot2));
        var manips = effects.HasEffect(EffectType.Manipulation);
        var perfection = effects.HasEffect(EffectType.TrainedPerfection);

        if (!perfection)
        {
            durability -= (int)MathF.Ceiling(durabilityA * wasteNots > 0 ? .5f : 1f);
            if (durability <= 0)
                return false;
        }

        if (manips)
            durability += 5;

        if (wasteNots > 0)
            wasteNots--;

        durability -= (int)MathF.Ceiling(durabilityB * wasteNots > 0 ? .5f : 1f);

        if (durability <= 0)
            return false;

        // If we can do the second action and still have durability left to survive to the next
        // step (even before the Manipulation modifier), we can certainly do the next action.
        return true;
    }

    public static bool VerifyDurability3(Simulator s, int durabilityA, int durabilityB) =>
        VerifyDurability3(durabilityA, durabilityB, s.Durability, s.ActiveEffects);
}
