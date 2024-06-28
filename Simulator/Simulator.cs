using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Craftimizer.Simulator;

public class Simulator
{
    public SimulationState State { init => state = value; }

    private SimulationState state;

    public SimulationInput Input => state.Input;
    public ref int ActionCount => ref state.ActionCount;
    public ref int StepCount => ref state.StepCount;
    public ref int Progress => ref state.Progress;
    public ref int Quality => ref state.Quality;
    public ref int Durability => ref state.Durability;
    public ref int CP => ref state.CP;
    public ref Condition Condition => ref state.Condition;
    public ref Effects ActiveEffects => ref state.ActiveEffects;
    public ref ActionStates ActionStates => ref state.ActionStates;

    public bool IsFirstStep => state.StepCount == 0;

    public virtual CompletionState CompletionState {
        get
        {
            if (Progress >= Input.Recipe.MaxProgress)
                return CompletionState.ProgressComplete;
            if (Durability <= 0)
                return CompletionState.NoMoreDurability;
            return CompletionState.Incomplete;
        }
    }
    public bool IsComplete => CompletionState != CompletionState.Incomplete;

    public SimulationState ExecuteUnchecked(in SimulationState state, ActionType action)
    {
        this.state = state;
        ExecuteUnchecked(action);
        return this.state;
    }

    public (ActionResponse Response, SimulationState NewState) Execute(in SimulationState state, ActionType action)
    {
        this.state = state;
        return (Execute(action), this.state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteUnchecked(ActionType action) =>
        action.Base().Use(this);

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
            if (action is ActionType.CarefulObservation or ActionType.HeartAndSoul && !Input.Stats.IsSpecialist)
                return ActionResponse.ActionNotUnlocked;
            if (baseAction.CPCost(this) > CP)
                return ActionResponse.NotEnoughCP;
            return ActionResponse.CannotUseAction;
        }

        baseAction.Use(this);

        return ActionResponse.UsedAction;
    }

    public (ActionResponse Response, SimulationState NewState, int FailedActionIdx) ExecuteMultiple(in SimulationState state, IEnumerable<ActionType> actions)
    {
        this.state = state;
        var i = 0;
        foreach(var action in actions)
        {
            var resp = Execute(action);
            if (resp != ActionResponse.UsedAction)
                return (resp, this.state, i);
            i++;
        }
        return (ActionResponse.UsedAction, this.state, -1);
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

    public virtual bool RollSuccessRaw(int successRate) =>
        successRate >= Input.Random.NextSingle() * 100f;

    public bool RollSuccess(int successRate) =>
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

    public void RestoreAllDurability()
    {
        Durability = Input.Recipe.MaxDurability;
    }


    public void RestoreCP(int amount)
    {
        CP += amount;

        if (CP > Input.Stats.CP)
            CP = Input.Stats.CP;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateSuccessRate(int successRate)
    {
        if (Condition == Condition.Centered)
            successRate += 25;
        return Math.Clamp(successRate, 0, 100);
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

    public int CalculateProgressGain(int efficiency, bool dryRun = true)
    {
        var buffModifier = 100;
        if (HasEffect(EffectType.MuscleMemory))
        {
            buffModifier += 100;
            if (!dryRun)
                RemoveEffect(EffectType.MuscleMemory);
        }
        if (HasEffect(EffectType.Veneration))
            buffModifier += 50;

        var conditionModifier = Condition switch
        {
            Condition.Malleable => 150,
            _ => 100
        };

        var progressGain = (int)((long)Input.BaseProgressGain * efficiency * conditionModifier * buffModifier / 1e6);
        return progressGain;
    }

    public int CalculateQualityGain(int efficiency, bool dryRun = true)
    {
        var buffModifier = 100;
        if (HasEffect(EffectType.GreatStrides))
        {
            buffModifier += 100;
            if (!dryRun)
                RemoveEffect(EffectType.GreatStrides);
        }
        if (HasEffect(EffectType.Innovation))
            buffModifier += 50;

        var iqModifier = 100 + (GetEffectStrength(EffectType.InnerQuiet) * 10);

        var conditionModifier = Condition switch
        {
            Condition.Poor => 50,
            Condition.Good => Input.Stats.HasSplendorousBuff ? 175 : 150,
            Condition.Excellent => 400,
            _ => 100,
        };

        var qualityGain = (int)((long)Input.BaseQualityGain * efficiency * conditionModifier * iqModifier * buffModifier / 1e8);
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
    public void IncreaseProgress(int efficiency) =>
        IncreaseProgressRaw(CalculateProgressGain(efficiency, false));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncreaseQuality(int efficiency) =>
        IncreaseQualityRaw(CalculateQualityGain(efficiency, false));
}
