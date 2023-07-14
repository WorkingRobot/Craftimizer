namespace Craftimizer.Simulator.Actions;

public abstract class BaseComboAction : BaseAction
{
    public abstract ActionType ActionTypeA { get; }
    public abstract ActionType ActionTypeB { get; }

    public sealed override ActionCategory Category => ActionCategory.Combo;

    protected bool BaseCanUse(Simulator s) =>
        base.CanUse(s);

    private static bool VerifyDurability2(int durabilityA, int durability, Effects effects)
    {
        var wasteNots = effects.HasEffect(EffectType.WasteNot) || effects.HasEffect(EffectType.WasteNot2);
        // -A
        durability -= (int)MathF.Ceiling(durabilityA * (wasteNots ? .5f : 1f));
        if (durability <= 0)
            return false;

        // If we can do the first action and still have durability left to survive to the next
        // step (even before the Manipulation modifier), we can certainly do the next action.
        return true;
    }

    public static bool VerifyDurability2(SimulationState s, int durabilityA) =>
        VerifyDurability2(durabilityA, s.Durability, s.ActiveEffects);

    public static bool VerifyDurability2(Simulator s, int durabilityA) =>
        VerifyDurability2(durabilityA, s.Durability, s.ActiveEffects);

    public static bool VerifyDurability3(int durabilityA, int durabilityB, int durability, Effects effects)
    {
        var wasteNots = Math.Max(effects.GetDuration(EffectType.WasteNot), effects.GetDuration(EffectType.WasteNot2));
        var manips = effects.HasEffect(EffectType.Manipulation);

        durability -= (int)MathF.Ceiling(durabilityA * wasteNots > 0 ? .5f : 1f);
        if (durability <= 0)
            return false;

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

    public static bool VerifyDurability3(SimulationState s, int durabilityA, int durabilityB) =>
        VerifyDurability3(durabilityA, durabilityB, s.Durability, s.ActiveEffects);
}
