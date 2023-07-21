using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Simulator;

public class Simulator
{
    protected SimulationState State;

    public SimulationInput Input => State.Input;
    public ref int ActionCount => ref State.ActionCount;
    public ref int StepCount => ref State.StepCount;
    public ref int Progress => ref State.Progress;
    public ref int Quality => ref State.Quality;
    public ref int Durability => ref State.Durability;
    public ref int CP => ref State.CP;
    public ref Condition Condition => ref State.Condition;
    public ref Effects ActiveEffects => ref State.ActiveEffects;
    public ref ActionStates ActionStates => ref State.ActionStates;

    public bool IsFirstStep => State.StepCount == 0;

    public CompletionState CompletionState => CalculateCompletionState(State);
    public virtual bool IsComplete => CompletionState != CompletionState.Incomplete;

    public IEnumerable<ActionType> AvailableActions => ActionUtils.AvailableActions(this);

    public Simulator(SimulationState state)
    {
        State = state;
    }

    public void SetState(SimulationState state)
    {
        State = state;
    }

    public (ActionResponse Response, SimulationState NewState) Execute(SimulationState state, ActionType action)
    {
        State = state;
        return (Execute(action), State);
    }

    private ActionResponse Execute(ActionType action)
    {
        if (IsComplete)
            return ActionResponse.SimulationComplete;

        var baseAction = action.Base();
        if (!baseAction.CanUse(this))
        {
            if (baseAction.Level > Input.Stats.Level)
                return ActionResponse.ActionNotUnlocked;
            if (action == ActionType.Manipulation && !Input.Stats.CanUseManipulation)
                return ActionResponse.ActionNotUnlocked;
            if (baseAction.CPCost(this) > CP)
                return ActionResponse.NotEnoughCP;
            return ActionResponse.CannotUseAction;
        }

        baseAction.Use(this);

        return ActionResponse.UsedAction;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEffectStrength(EffectType effect) =>
        ActiveEffects.GetStrength(effect);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEffectDuration(EffectType effect) =>
        ActiveEffects.GetDuration(effect);

    public void AddEffect(EffectType effect, int duration)
    {
        if (Condition == Condition.Primed)
            duration += 2;

        // Duration will be decreased in the next step, so we need to add 1
        duration++;

        ActiveEffects.SetDuration(effect, (byte)duration);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StrengthenEffect(EffectType effect) =>
        ActiveEffects.Strengthen(effect);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveEffect(EffectType effect) =>
        ActiveEffects.SetDuration(effect, 0);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasEffect(EffectType effect) =>
        ActiveEffects.HasEffect(effect);

    public virtual bool RollSuccessRaw(float successRate) =>
        successRate >= Input.Random.NextSingle();

    public bool RollSuccess(float successRate) =>
        RollSuccessRaw(CalculateSuccessRate(successRate));

    public void IncreaseStepCount()
    {
        StepCount++;
        StepCondition();
    }

    private static float GetConditionChance(SimulationInput input, Condition condition) =>
        condition switch
        {
            Condition.Good => input.Recipe.IsExpert ? 0.12f : (input.Stats.Level >= 63 ? 0.15f : 0.18f),
            Condition.Excellent => 0.04f,
            Condition.Centered => 0.15f,
            Condition.Sturdy => 0.15f,
            Condition.Pliant => 0.10f,
            Condition.Malleable => 0.13f,
            Condition.Primed => 0.15f,
            Condition.GoodOmen => 0.12f, // https://github.com/ffxiv-teamcraft/simulator/issues/77
            _ => 0.00f
        };

    public virtual Condition GetNextRandomCondition()
    {
        var conditionChance = Input.Random.NextSingle();

        foreach (var condition in Input.AvailableConditions)
            if ((conditionChance -= GetConditionChance(Input, condition)) < 0)
                return condition;

        return Condition.Normal;
    }

    public void StepCondition()
    {
        Condition = Condition switch
        {
            Condition.Poor => Condition.Normal,
            Condition.Good => Condition.Normal,
            Condition.Excellent => Condition.Poor,
            Condition.GoodOmen => Condition.Good,
            _ => GetNextRandomCondition()
        };
    }

    public void RestoreDurability(int amount)
    {
        Durability += amount;

        if (Durability > Input.Recipe.MaxDurability)
            Durability = Input.Recipe.MaxDurability;
    }

    public void RestoreCP(int amount)
    {
        CP += amount;

        if (CP > Input.Stats.CP)
            CP = Input.Stats.CP;
    }

    public float CalculateSuccessRate(float successRate)
    {
        if (Condition == Condition.Centered)
            successRate += 0.25f;
        return Math.Clamp(successRate, 0, 1);
    }

    public int CalculateDurabilityCost(int amount)
    {
        var amt = (double)amount;
        if (HasEffect(EffectType.WasteNot) || HasEffect(EffectType.WasteNot2))
            amt /= 2;
        if (Condition == Condition.Sturdy)
            amt /= 2;
        return (int)Math.Ceiling(amt);
    }

    public int CalculateCPCost(int amount)
    {
        var amt = (double)amount;
        if (Condition == Condition.Pliant)
            amt /= 2;
        return (int)Math.Ceiling(amt);
    }

    public int CalculateProgressGain(float efficiency, bool dryRun = true)
    {
        var buffModifier = 1.00f;
        if (HasEffect(EffectType.MuscleMemory))
        {
            buffModifier += 1.00f;
            if (!dryRun)
                RemoveEffect(EffectType.MuscleMemory);
        }
        if (HasEffect(EffectType.Veneration))
            buffModifier += 0.50f;

        var conditionModifier = Condition switch
        {
            Condition.Malleable => 1.50f,
            _ => 1.00f
        };

        var progressGain = (int)(Input.BaseProgressGain * efficiency * conditionModifier * buffModifier);
        return progressGain;
    }

    public int CalculateQualityGain(float efficiency, bool dryRun = true)
    {
        var buffModifier = 1.00f;
        if (HasEffect(EffectType.GreatStrides))
        {
            buffModifier += 1.00f;
            if (!dryRun)
                RemoveEffect(EffectType.GreatStrides);
        }
        if (HasEffect(EffectType.Innovation))
            buffModifier += 0.50f;

        buffModifier *= 1 + (GetEffectStrength(EffectType.InnerQuiet) * 0.10f);

        var conditionModifier = Condition switch
        {
            Condition.Poor => 0.50f,
            Condition.Good => Input.Stats.HasSplendorousBuff ? 1.75f : 1.50f,
            Condition.Excellent => 4.00f,
            _ => 1.00f,
        };

        var qualityGain = (int)(Input.BaseQualityGain * efficiency * conditionModifier * buffModifier);
        return qualityGain;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReduceDurabilityRaw(int amount) =>
        Durability -= amount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReduceCPRaw(int amount) =>
        CP -= amount;

    public void IncreaseProgressRaw(int progressGain)
    {
        Progress += progressGain;

        if (HasEffect(EffectType.FinalAppraisal) && Progress >= Input.Recipe.MaxProgress)
        {
            Progress = Input.Recipe.MaxProgress - 1;
            RemoveEffect(EffectType.FinalAppraisal);
        }
    }

    public void IncreaseQualityRaw(int qualityGain)
    {
        Quality += qualityGain;

        if (Input.Stats.Level >= 11)
            StrengthenEffect(EffectType.InnerQuiet);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReduceDurability(int amount) =>
        ReduceDurabilityRaw(CalculateDurabilityCost(amount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReduceCP(int amount) =>
        ReduceCPRaw(CalculateCPCost(amount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncreaseProgress(float efficiency) =>
        IncreaseProgressRaw(CalculateProgressGain(efficiency, false));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncreaseQuality(float efficiency) =>
        IncreaseQualityRaw(CalculateQualityGain(efficiency, false));

    public static CompletionState CalculateCompletionState(SimulationState state)
    {
        if (state.Progress >= state.Input.Recipe.MaxProgress)
            return CompletionState.ProgressComplete;
        if (state.Durability <= 0)
            return CompletionState.NoMoreDurability;
        return CompletionState.Incomplete;
    }
}
