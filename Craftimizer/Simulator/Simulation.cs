using Craftimizer.Simulator.Actions;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Craftimizer.Simulator;

public class Simulation
{
    public CharacterStats Stats { get; }
    public Recipe Recipe { get; }
    public RecipeLevelTable RecipeTable => Recipe.RecipeLevelTable.Value!;
    public int RLvl => (int)RecipeTable.RowId;
    public readonly Condition[] AvailableConditions;

    public int MaxDurability => RecipeTable.Durability * Recipe.DurabilityFactor / 100;
    public int MaxQuality => (int)RecipeTable.Quality * Recipe.QualityFactor / 100;
    public int MaxProgress => RecipeTable.Difficulty * Recipe.DifficultyFactor / 100;

    public bool IsComplete { get; private set; }
    public int StepCount { get; private set; }
    public int Progress { get; private set; }
    public int Quality { get; private set; }
    public int Durability { get; private set; }
    public int CP { get; private set; }
    public Condition Condition { get; private set; }
    public List<Effect> ActiveEffects { get; } = new();
    public List<BaseAction> ActionHistory { get; } = new();

    // https://github.com/ffxiv-teamcraft/simulator/blob/0682dfa76043ff4ccb38832c184d046ceaff0733/src/model/tables.ts#L2
    private static readonly int[] HQPercentTable = {
        1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8,
        9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 16, 16, 17, 17,
        17, 18, 18, 18, 19, 19, 20, 20, 21, 22, 23, 24, 26, 28, 31, 34, 38, 42, 47, 52, 58, 64, 68, 71,
        74, 76, 78, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 94, 96, 98, 100
    };
    public int HQPercent => HQPercentTable[(int)Math.Clamp((float)Quality / MaxQuality * 100, 0, 100)];

    public bool IsFirstStep => StepCount == 0;

    private Random Random { get; } = new();

    public Simulation(CharacterStats stats, Recipe recipe)
    {
        Stats = stats;
        Recipe = recipe;
        IsComplete = false;
        StepCount = 0;
        Progress = 0;
        Quality = 0;
        Durability = MaxDurability;
        CP = Stats.CP;
        Condition = Condition.Normal;
        AvailableConditions = ConditionUtils.GetPossibleConditions(RecipeTable.ConditionsFlag);
    }

    public ActionResponse Execute(BaseAction action)
    {
        if (IsComplete)
            return ActionResponse.SimulationComplete;

        if (!action.CanUse)
        {
            if (action.Level > Stats.Level)
                return ActionResponse.ActionNotUnlocked;
            if (action.CPCost > CP)
                return ActionResponse.NotEnoughCP;
            return ActionResponse.CannotUseAction;
        }

        action.Use();
        ActionHistory.Add(action);

        for (var i = 0; i < ActiveEffects.Count; ++i)
        {
            var effect = ActiveEffects[i];
            effect.Duration--;
            if (effect.Duration == 0)
            {
                ActiveEffects.RemoveAt(i);
                --i;
            }
        }

        if (Progress >= MaxProgress)
        {
            IsComplete = true;
            return ActionResponse.ProgressComplete;
        }
        if (Durability <= 0)
        {
            IsComplete = true;
            return ActionResponse.NoMoreDurability;
        }

        return ActionResponse.UsedAction;
    }

    public ActionResponse Execute<T>() where T : BaseAction =>
        Execute((T)Activator.CreateInstance(typeof(T), this)!);

    public Effect? GetEffect(EffectType effect) =>
        ActiveEffects.FirstOrDefault(e => e.Type == effect);

    public void AddEffect(EffectType effect, int? duration = null, int? strength = null)
    {
        if (Condition == Condition.Primed && duration != null)
            duration += 2;

        // Duration will be decreased in the next step, so we need to add 1
        if (duration != null)
            duration++;

        var currentEffect = GetEffect(effect);
        if (currentEffect != null) {
            currentEffect.Duration = duration;
            currentEffect.Strength = strength;
        }
        else
            ActiveEffects.Add(new Effect { Type = effect, Duration = duration, Strength = strength });
    }

    public void StrengthenEffect(EffectType effect, int? duration = null)
    {
        if (duration != null)
            duration += 1;

        var currentEffect = GetEffect(effect);
        if (currentEffect != null)
        {
            if (effect.Status().MaxStacks > currentEffect.Strength)
                currentEffect.Strength++;
        }
        else
            AddEffect(effect, duration, 1);
    }

    public void RemoveEffect(EffectType effect) =>
        ActiveEffects.RemoveAll(e => e.Type == effect);

    public bool HasEffect(EffectType effect) =>
        ActiveEffects.Any(e => e.Type == effect);

    public bool IsPreviousAction<T>(int stepsBack = 1) where T : BaseAction =>
        ActionHistory.Count >= stepsBack && ActionHistory[^stepsBack] is T;

    public int CountPreviousAction<T>() where T : BaseAction =>
        ActionHistory.Count(x => x is T);

    public bool RollSuccessRaw(float successRate) =>
        successRate >= Random.NextSingle();

    public bool RollSuccess(float successRate) =>
        RollSuccessRaw(CalculateSuccessRate(successRate));

    public void IncreaseStepCount()
    {
        StepCount++;
        StepCondition();
    }

    private float GetConditionChance(Condition condition) =>
        condition switch
        {
            Condition.Good => Recipe.IsExpert ? 0.12f : (Stats.Level >= 63 ? 0.15f : 0.18f),
            Condition.Excellent => 0.04f,
            Condition.Centered => 0.15f,
            Condition.Sturdy => 0.15f,
            Condition.Pliant => 0.10f,
            Condition.Malleable => 0.13f,
            Condition.Primed => 0.15f,
            Condition.GoodOmen => 0.12f, // https://github.com/ffxiv-teamcraft/simulator/issues/77
            _ => 0.00f
        };

    public void StepCondition()
    {
        var conditionChance = Random.NextSingle();

        Condition = Condition switch {
            Condition.Poor => Condition.Normal,
            Condition.Good => Condition.Normal,
            Condition.Excellent => Condition.Poor,
            Condition.GoodOmen => Condition.Good,
            _ => AvailableConditions.FirstOrDefault(c => (conditionChance -= GetConditionChance(c)) < 0, Condition.Normal)
        };
    }

    public void RestoreDurability(int amount)
    {
        Durability += amount;

        if (Durability > MaxDurability)
            Durability = MaxDurability;
    }

    public void RestoreCP(int amount)
    {
        CP += amount;
        
        if (CP > Stats.CP)
            CP = Stats.CP;
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

        // https://github.com/NotRanged/NotRanged.github.io/blob/0f4aee074f969fb05aad34feaba605057c08ffd1/app/js/ffxivcraftmodel.js#L88
        var baseIncrease = (Stats.Craftsmanship * 10f / RecipeTable.ProgressDivider) + 2;
        if (Stats.CLvl <= RLvl)
            baseIncrease *= RecipeTable.ProgressModifier / 100f;
        baseIncrease = MathF.Floor(baseIncrease);

        var progressGain = (int)(baseIncrease * efficiency * conditionModifier * buffModifier);
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

        buffModifier *= 1 + ((GetEffect(EffectType.InnerQuiet)?.Strength ?? 0) * 0.10f);

        var conditionModifier = Condition switch
        {
            Condition.Poor => 0.50f,
            Condition.Good => Stats.HasRelic ? 1.75f : 1.50f,
            Condition.Excellent => 4.00f,
            _ => 1.00f,
        };

        var baseIncrease = (Stats.Control * 10f / RecipeTable.QualityDivider) + 35;
        if (Stats.CLvl <= RLvl)
            baseIncrease *= RecipeTable.QualityModifier / 100f;
        baseIncrease = MathF.Floor(baseIncrease);

        var qualityGain = (int)(baseIncrease * efficiency * conditionModifier * buffModifier);
        return qualityGain;
    }

    public void ReduceDurabilityRaw(int amount) =>
        Durability -= amount;

    public void ReduceCPRaw(int amount) =>
        CP -= amount;

    public void IncreaseProgressRaw(int progressGain)
    {
        Progress += progressGain;

        if (HasEffect(EffectType.FinalAppraisal) && Progress >= MaxProgress)
        {
            Progress = MaxProgress - 1;
            RemoveEffect(EffectType.FinalAppraisal);
        }
    }

    public void IncreaseQualityRaw(int qualityGain)
    {
        Quality += qualityGain;

        if (Stats.Level >= 11)
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
