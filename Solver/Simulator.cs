using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver;

internal sealed class Simulator : SimulatorNoRandom
{
    // Per-action metadata that is constant for the lifetime of the pool (never depends on
    // simulation state). Precomputed once so the heuristic hot loop never reads virtual
    // fields/methods off the BaseAction object.
    private readonly struct PoolEntry
    {
        public readonly BaseAction Data;
        public readonly ActionType Action;
        public readonly int DurabilityCost;
        public readonly int SuccessRate;
        public readonly ActionCategory Category;
        public readonly bool IncreasesProgress;
        public readonly bool IncreasesQuality;

        public PoolEntry(BaseAction data, ActionType action, Simulator s)
        {
            Data = data;
            Action = action;
            DurabilityCost = data.DurabilityCost;
            SuccessRate = data.SuccessRate(s); // never state-dependent (no action overrides it)
            Category = data.Category;
            IncreasesProgress = data.IncreasesProgress;
            IncreasesQuality = data.IncreasesQuality;
        }
    }

    // State-invariant values hoisted out of the per-action loop. Computed once per
    // AvailableActionsHeuristic call rather than ~28 times (once per action in the pool).
    private readonly struct StepContext
    {
        public readonly bool Centered;
        public readonly bool QualityReached;
        public readonly bool IsDifficult;
        public readonly bool HasMuscleMemory;
        public readonly bool FirstTurnGate;     // IsFirstStep && Level>=69 && isDifficult && CP>=6
        public readonly ActionProc Combo;
        public readonly bool HasVeneration;
        public readonly bool HasInnovation;
        public readonly bool HasManipulation;
        public readonly bool HasGreatStrides;
        // durability cost factors (mirror Simulator.CalculateDurabilityCost dry-run exactly)
        public readonly bool TrainedPerfection;
        public readonly bool WasteNot;
        public readonly bool Sturdy;

        public readonly int Durability;
        public readonly int MaxDurability;
        public readonly int CP;
        public readonly int Quality;
        public readonly int MaxQuality;
        public readonly int Progress;
        public readonly int MaxProgress;
        public readonly int InnerQuiet;
        public readonly int VenerationDuration;
        public readonly int InnovationDuration;

        // progress-gain modifiers (mirror Simulator.CalculateProgressGain dry-run exactly)
        public readonly long BaseProgressGain;
        public readonly int ProgressBuffModifier;
        public readonly int ProgressConditionModifier;

        public StepContext(Simulator s)
        {
            var input = s.Input;
            var recipe = input.Recipe;
            var stats = input.Stats;

            Centered = s.Condition == Condition.Centered;
            Quality = s.Quality;
            MaxQuality = recipe.MaxQuality;
            QualityReached = Quality >= MaxQuality;

            IsDifficult = stats.Level - recipe.ClassJobLevel < 10 || recipe.IsExpert;
            HasMuscleMemory = s.HasEffect(EffectType.MuscleMemory);

            CP = s.CP;
            FirstTurnGate = s.IsFirstStep && stats.Level >= 69 && IsDifficult && CP >= 6;

            Combo = s.ActionStates.Combo;
            HasVeneration = s.HasEffect(EffectType.Veneration);
            HasInnovation = s.HasEffect(EffectType.Innovation);
            HasManipulation = s.HasEffect(EffectType.Manipulation);
            HasGreatStrides = s.HasEffect(EffectType.GreatStrides);

            TrainedPerfection = s.HasEffect(EffectType.TrainedPerfection);
            WasteNot = s.HasEffect(EffectType.WasteNot) || s.HasEffect(EffectType.WasteNot2);
            Sturdy = s.Condition is Condition.Sturdy or Condition.Robust;

            Durability = s.Durability;
            MaxDurability = recipe.MaxDurability;
            Progress = s.Progress;
            MaxProgress = recipe.MaxProgress;
            InnerQuiet = s.GetEffectStrength(EffectType.InnerQuiet);
            VenerationDuration = s.GetEffectDuration(EffectType.Veneration);
            InnovationDuration = s.GetEffectDuration(EffectType.Innovation);

            BaseProgressGain = input.BaseProgressGain;
            var buffModifier = 100;
            if (HasMuscleMemory)
                buffModifier += 100;
            if (HasVeneration)
                buffModifier += 50;
            ProgressBuffModifier = buffModifier;
            ProgressConditionModifier = s.Condition == Condition.Malleable ? 150 : 100;
        }

        // Mirrors Simulator.CalculateSuccessRate (dry) for a constant base rate.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsFullSuccess(int successRate)
        {
            if (Centered)
                successRate += 25;
            return Math.Clamp(successRate, 0, 100) == 100;
        }

        // Mirrors Simulator.CalculateDurabilityCost(amount, dryRun: true) exactly, but with
        // integer arithmetic: the divisor is always a power of two (1/2/4), so the double
        // Math.Ceiling reduces to an exact integer ceil-divide.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int DurabilityCost(int amount)
        {
            if (amount == 0 || TrainedPerfection)
                return 0;
            // divisor = (WasteNot ? 2 : 1) * (Sturdy ? 2 : 1)
            var shift = (WasteNot ? 1 : 0) + (Sturdy ? 1 : 0);
            var divisor = 1 << shift;
            return (amount + divisor - 1) / divisor;
        }

        // Mirrors Simulator.CalculateProgressGain(efficiency, dryRun: true) exactly.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int ProgressGain(int efficiency) =>
            (int)(BaseProgressGain * efficiency * ProgressConditionModifier * ProgressBuffModifier / 1e6);
    }

    private readonly PoolEntry[] actionPool;
    private readonly int trainedEyeIndex; // index of TrainedEye in actionPool, or -1
    private readonly int maxStepCount;

    public override CompletionState CompletionState
    {
        get
        {
            var b = base.CompletionState;
            if (b == CompletionState.Incomplete && (ActionCount + 1) >= maxStepCount)
                return CompletionState.MaxActionCountReached;
            return b;
        }
    }

    public Simulator(ActionType[] actionPool, int maxStepCount, SimulationState? filteringState = null)
    {
        var pool = actionPool.Select(x => (x.Base(), x));
        if (filteringState is { } state)
        {
            State = state;
            pool = pool.Where(x => x.Item1.IsPossible(this));
        }
        this.actionPool = [.. pool.OrderBy(x => x.x).Select(x => new PoolEntry(x.Item1, x.x, this))];
        this.trainedEyeIndex = Array.FindIndex(this.actionPool, e => e.Action == ActionType.TrainedEye);
        this.maxStepCount = maxStepCount;
    }

    // Picks ONE uniform-random valid action for a rollout playout step WITHOUT materializing the
    // full ActionSet (the dominant cost when only one action is needed). Distribution is identical
    // to building AvailableActionsHeuristic(strict: true) and calling ActionSet.SelectRandom on it.
    // Returns false when no action is valid (== NoMoreActions terminal). Caller must only invoke
    // this on an Incomplete state.
    public bool TryPickRolloutAction(Random random, out ActionType action)
    {
        var ctx = new StepContext(this);
        var pool = actionPool;
        var n = pool.Length;

        // Trained Eye dominates the set when usable (mirrors AvailableActionsHeuristic).
        if (trainedEyeIndex >= 0 && ShouldUseAction(in ctx, in pool[trainedEyeIndex]))
        {
            action = ActionType.TrainedEye;
            return true;
        }

#if IS_DETERMINISTIC
        // First valid action in ascending pool order == First() of the valid set, so the
        // deterministic fingerprint stays byte-identical to the old AvailableActionsHeuristic path.
        for (var i = 0; i < n; i++)
        {
            if (i == trainedEyeIndex)
                continue;
            if (ShouldUseAction(in ctx, in pool[i]))
            {
                action = pool[i].Action;
                return true;
            }
        }
        action = default;
        return false;
#else
        // Rejection sampling: uniform over the valid set, same distribution as ActionSet.SelectRandom.
        var attempts = n * 2;
        for (var a = 0; a < attempts; a++)
        {
            var idx = random.Next(n);
            if (idx == trainedEyeIndex)
                continue;
            if (ShouldUseAction(in ctx, in pool[idx]))
            {
                action = pool[idx].Action;
                return true;
            }
        }
        // Sparse/empty valid set: fall back to a single scan that picks uniformly or reports empty.
        Span<int> valid = stackalloc int[n];
        var count = 0;
        for (var i = 0; i < n; i++)
        {
            if (i == trainedEyeIndex)
                continue;
            if (ShouldUseAction(in ctx, in pool[i]))
                valid[count++] = i;
        }
        if (count == 0)
        {
            action = default;
            return false;
        }
        action = pool[valid[random.Next(count)]].Action;
        return true;
#endif
    }

    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L146
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CouldUseAction(in StepContext ctx, in PoolEntry entry)
    {
        if (!ctx.IsFullSuccess(entry.SuccessRate))
            return false;

        // don't allow quality moves at max quality
        if (ctx.QualityReached && entry.IncreasesQuality)
            return false;

        return entry.Data.CouldUse(this);
    }

    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L146
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // It's just a bunch of if statements, I would assume this is actually quite simple to follow
#pragma warning disable MA0051 // Method is too long
    private bool ShouldUseAction(in StepContext ctx, in PoolEntry entry)
#pragma warning restore MA0051 // Method is too long
    {
        var action = entry.Action;

        if (!ctx.IsFullSuccess(entry.SuccessRate))
            return false;

        // don't allow quality moves at max quality
        if (ctx.QualityReached && entry.IncreasesQuality)
            return false;

        // always use Trained Eye if it's available
        if (action == ActionType.TrainedEye)
            return entry.Data.CouldUse(this);

        // don't allow quality moves under Muscle Memory for difficult crafts
        if (ctx.IsDifficult &&
            ctx.HasMuscleMemory &&
            entry.IncreasesQuality)
            return false;

        // use First Turn actions if it's available and the craft is difficult
        if (ctx.FirstTurnGate &&
            entry.Category != ActionCategory.FirstTurn)
            return false;

        // don't allow combo actions if the combo is already in progress
        if (ctx.Combo != ActionProc.None &&
            (action is ActionType.StandardTouchCombo or ActionType.AdvancedTouchCombo or ActionType.RefinedTouchCombo))
            return false;

        // when combo'd, only allow Advanced Touch
        if (ctx.Combo == ActionProc.AdvancedTouch && action is not ActionType.AdvancedTouch)
            return false;

        // don't allow pure quality moves under Veneration
        if (ctx.HasVeneration &&
            !entry.IncreasesProgress &&
            entry.IncreasesQuality)
            return false;

        var durability = ctx.DurabilityCost(entry.DurabilityCost);

        // don't allow pure quality moves when it won't be able to finish the craft
        if (!entry.IncreasesProgress && durability >= ctx.Durability)
            return false;

        if (entry.IncreasesProgress)
        {
            var progressIncrease = ctx.ProgressGain(entry.Data.Efficiency(this));
            var wouldFinish = ctx.Progress + progressIncrease >= ctx.MaxProgress;

            if (wouldFinish)
            {
                // don't allow finishing the craft if there is significant quality remaining
                if (ctx.Quality * 5 < ctx.MaxQuality)
                    return false;
            }
            else
            {
                // don't allow pure progress moves under Innovation, if it wouldn't finish the craft
                if (ctx.HasInnovation &&
                    !entry.IncreasesQuality &&
                    entry.IncreasesProgress)
                    return false;
            }
        }

        if (action is ActionType.ByregotsBlessing &&
            ctx.InnerQuiet <= 1)
            return false;

        // use of Waste Not should be efficient
        if ((action is ActionType.WasteNot or ActionType.WasteNot2) &&
            ctx.WasteNot)
            return false;

        // don't Observe when Advanced Touch is impossible (7 + 18)
        if (action is ActionType.Observe && ctx.CP < 25)
            return false;

        // don't allow Refined Touch without a combo
        if (action is ActionType.RefinedTouch &&
            ctx.Combo != ActionProc.UsedBasicTouch)
            return false;

        // don't allow Immaculate Mends that are too inefficient
        if (action is ActionType.ImmaculateMend &&
            (ctx.MaxDurability - durability <= 45 || ctx.HasManipulation))
            return false;

        // don't allow buffs too early
        if (action is ActionType.MastersMend &&
            ctx.MaxDurability - durability < 25)
            return false;

        if (action is ActionType.Manipulation &&
            ctx.HasManipulation)
            return false;

        if (action is ActionType.GreatStrides &&
            ctx.HasGreatStrides)
            return false;

        if ((action is ActionType.Veneration or ActionType.Innovation) &&
            (ctx.VenerationDuration > 1 || ctx.InnovationDuration > 1))
            return false;

        if (action is ActionType.QuickInnovation &&
            ctx.Quality * 3 <= ctx.MaxQuality)
            return false;

        return entry.Data.CouldUse(this);
    }

    // https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/craft_state.rs#L137
    public ActionSet AvailableActionsHeuristic(bool strict)
    {
        if (IsComplete)
            return new();

        var ctx = new StepContext(this);
        var ret = new ActionSet();
        if (strict)
        {
            foreach (ref readonly var entry in actionPool.AsSpan())
            {
                if (ShouldUseAction(in ctx, in entry))
                    ret.AddAction(entry.Action);
            }

            // If Trained Eye is possible, *always* use Trained Eye
            if (ret.HasAction(ActionType.TrainedEye))
            {
                ret = new();
                ret.AddAction(ActionType.TrainedEye);
            }
        }
        else
        {
            foreach (ref readonly var entry in actionPool.AsSpan())
                if (CouldUseAction(in ctx, in entry))
                    ret.AddAction(entry.Action);
        }

        return ret;
    }
}
