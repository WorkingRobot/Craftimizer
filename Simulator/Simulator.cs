using Craftimizer.Simulator.Actions;

namespace Craftimizer.Simulator;

public class Simulator
{
    public SimulationInput Input { get; private set; }
    public int StepCount { get; private set; }
    public int Progress { get; private set; }
    public int Quality { get; private set; }
    public int Durability { get; private set; }
    public int CP { get; private set; }
    public Condition Condition { get; private set; }
    public Effects ActiveEffects;
    public List<ActionType> ActionHistory { get; private set; }

    public bool IsFirstStep => StepCount == 0;

    public CompletionState CompletionState
    {
        get
        {
            if (Progress >= Input.Recipe.MaxProgress)
                return CompletionState.ProgressComplete;
            if (Durability <= 0)
                return CompletionState.NoMoreDurability;
            return CompletionState.Incomplete;
        }
    }
    public virtual bool IsComplete => CompletionState != CompletionState.Incomplete;

    public IEnumerable<ActionType> AvailableActions => ActionUtils.AvailableActions(this);

#pragma warning disable CS8618 // Emplace sets all the fields already
    public Simulator(SimulationState state)
#pragma warning restore CS8618
    {
        Emplace(state);
    }

    private void Emplace(SimulationState state)
    {
        Input = state.Input;
        StepCount = state.StepCount;
        Progress = state.Progress;
        Quality = state.Quality;
        Durability = state.Durability;
        CP = state.CP;
        Condition = state.Condition;
        ActiveEffects = state.ActiveEffects;
        ActionHistory = new(state.ActionHistory);
    }

    private SimulationState Displace() => new()
        {
            Input = Input,
            StepCount = StepCount,
            Progress = Progress,
            Quality = Quality,
            Durability = Durability,
            CP = CP,
            Condition = Condition,
            ActiveEffects = ActiveEffects,
            ActionHistory = ActionHistory!,
        };

    public (ActionResponse Response, SimulationState NewState) Execute(SimulationState state, ActionType action)
    {
        Emplace(state);
        return (Execute(action), Displace());
    }

    private ActionResponse Execute(ActionType action)
    {
        if (IsComplete)
            return ActionResponse.SimulationComplete;

        var baseAction = action.With(this);
        if (!baseAction.CanUse)
        {
            if (baseAction.Level > Input.Stats.Level)
                return ActionResponse.ActionNotUnlocked;
            if (baseAction.CPCost > CP)
                return ActionResponse.NotEnoughCP;
            return ActionResponse.CannotUseAction;
        }

        baseAction.Use();
        ActionHistory!.Add(action);

        ActiveEffects.DecrementDuration();

        return ActionResponse.UsedAction;
    }

    public int GetEffectStrength(EffectType effect) =>
        ActiveEffects.GetStrength(effect);

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

    public void StrengthenEffect(EffectType effect) =>
        ActiveEffects.Strengthen(effect);

    public void RemoveEffect(EffectType effect) =>
        ActiveEffects.SetDuration(effect, 0);

    public bool HasEffect(EffectType effect) =>
        ActiveEffects.HasEffect(effect);

    public bool IsPreviousAction(ActionType action, int stepsBack = 1) =>
        ActionHistory!.Count >= stepsBack && ActionHistory[^stepsBack] == action;

    public int CountPreviousAction(ActionType action) =>
        ActionHistory!.Count(a => a == action);

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

    private Condition GetNextRandomCondition()
    {
        var conditionChance = Input.Random.NextSingle();

        foreach (var condition in Input.AvailableConditions)
            if ((conditionChance -= GetConditionChance(Input, condition)) < 0)
                return condition;

        return Condition.Normal;
    }

    public virtual void StepCondition()
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
            Condition.Good => Input.Stats.HasRelic ? 1.75f : 1.50f,
            Condition.Excellent => 4.00f,
            _ => 1.00f,
        };

        var qualityGain = (int)(Input.BaseQualityGain * efficiency * conditionModifier * buffModifier);
        return qualityGain;
    }

    public void ReduceDurabilityRaw(int amount) =>
        Durability -= amount;

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

    public void ReduceDurability(int amount) =>
        ReduceDurabilityRaw(CalculateDurabilityCost(amount));

    public void ReduceCP(int amount) =>
        ReduceCPRaw(CalculateCPCost(amount));

    public void IncreaseProgress(float efficiency) =>
        IncreaseProgressRaw(CalculateProgressGain(efficiency, false));

    public void IncreaseQuality(float efficiency) =>
        IncreaseQualityRaw(CalculateQualityGain(efficiency, false));
}
