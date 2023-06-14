using Craftimizer.Simulator.Actions;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;

namespace Craftimizer.Simulator;

public class Simulation
{
    public CharacterStats Stats { get; }
    public Recipe Recipe { get; }
    public RecipeLevelTable RecipeTable => Recipe.RecipeLevelTable.Value!;
    public int RLvl => (int)RecipeTable.RowId;

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
    public List<(Effect effect, int strength, int stepsLeft)> ActiveEffects { get; } = new();
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
            var (effect, strength, stepsLeft) = ActiveEffects[i];
            if (stepsLeft == 1)
            {
                ActiveEffects.RemoveAt(i);
                --i;
            }
            else
                ActiveEffects[i] = (effect, strength, stepsLeft - 1);
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

    public (int Strength, int Duration)? GetEffect(Effect effect)
    {
        var idx = ActiveEffects.FindIndex(x => x.effect == effect);
        if (idx == -1)
            return null;
        var (_, strength, duration) = ActiveEffects[idx];
        return (strength, duration);
    }

    public void AddEffect(Effect effect, int duration, int strength = 1)
    {
        // Duration will be decreased in the next step, so we need to add 1
        duration += 1;

        var idx = ActiveEffects.FindIndex(x => x.effect == effect);
        if (idx == -1)
            ActiveEffects.Add((effect, strength, duration));
        else
            ActiveEffects[idx] = (effect, strength, duration);
    }

    public void StrengthenEffect(Effect effect, int duration = -1)
    {
        var idx = ActiveEffects.FindIndex(x => x.effect == effect);
        if (idx == -1)
            ActiveEffects.Add((effect, 1, duration));
        else
            ActiveEffects[idx] = (effect, ActiveEffects[idx].strength + 1, duration);
    }

    public void RemoveEffect(Effect effect)
    {
        var idx = ActiveEffects.FindIndex(x => x.effect == effect);
        if (idx != -1)
            ActiveEffects.RemoveAt(idx);
    }

    public bool HasEffect(Effect effect) => GetEffect(effect) != null;

    public BaseAction? GetPreviousAction(int stepsBack = 1) =>
        ActionHistory.Count < stepsBack ? null : ActionHistory[^stepsBack];

    public bool RollSuccess(float successRate) =>
        successRate >= Random.NextSingle();

    public void IncreaseStepCount() =>
        StepCount++;

    public void ReduceDurability(int amount)
    {
        if (HasEffect(Effect.WasteNot) || HasEffect(Effect.WasteNot2))
            amount /= 2;
        Durability -= amount;
    }

    public void RestoreDurability(int amount)
    {
        Durability += amount;

        if (Durability > MaxDurability)
            Durability = MaxDurability;
    }

    public void ReduceCP(int amount)
    {
        CP -= amount;
    }

    public void RestoreCP(int amount)
    {
        CP += amount;
        
        if (CP > Stats.CP)
            CP = Stats.CP;
    }

    public int CalculateProgressGain(float efficiency)
    {
        var buffModifier = 1.00f;
        if (HasEffect(Effect.MuscleMemory))
        {
            buffModifier += 1.00f;
            RemoveEffect(Effect.MuscleMemory);
        }
        if (HasEffect(Effect.Veneration))
            buffModifier += 0.50f;

        // https://github.com/NotRanged/NotRanged.github.io/blob/0f4aee074f969fb05aad34feaba605057c08ffd1/app/js/ffxivcraftmodel.js#L88
        PluginLog.LogDebug($"Efficiency: {efficiency}");
        PluginLog.LogDebug($"Buff Modifier: {buffModifier}");
        var baseIncrease = (Stats.Craftsmanship * 10f / RecipeTable.ProgressDivider) + 2;
        PluginLog.LogDebug($"Increase: {baseIncrease}");
        if (Stats.CLvl <= RLvl)
        {
            baseIncrease *= RecipeTable.ProgressModifier / 100f;
            PluginLog.LogDebug($"Boosted Increase: {baseIncrease}");
        }
        baseIncrease = MathF.Floor(baseIncrease);
        PluginLog.LogDebug($"Adj. Increase: {baseIncrease}");

        var progressGain = (int)(baseIncrease * efficiency * buffModifier);
        PluginLog.LogDebug($"Progress Gain: {progressGain}");
        return progressGain;
    }

    public int CalculateQualityGain(float efficiency)
    {
        var buffModifier = 1.00f;
        if (HasEffect(Effect.GreatStrides))
        {
            buffModifier += 1.00f;
            RemoveEffect(Effect.GreatStrides);
        }
        if (HasEffect(Effect.Innovation))
            buffModifier += 0.50f;

        buffModifier *= 1 + ((GetEffect(Effect.InnerQuiet)?.Strength ?? 0) * 0.10f);

        var conditionModifier = Condition switch
        {
            Condition.Poor => 0.50f,
            Condition.Good => 1.50f, // 1.75f if relic tool
            Condition.Excellent => 4.00f,
            _ => 1.00f,
        };

        PluginLog.LogDebug($"Efficiency: {efficiency}");
        PluginLog.LogDebug($"Buff Modifier: {buffModifier}");
        PluginLog.LogDebug($"Cond Modifier: {conditionModifier}");
        var baseIncrease = (Stats.Control * 10f / RecipeTable.QualityDivider) + 35;
        PluginLog.LogDebug($"Increase: {baseIncrease}");
        if (Stats.CLvl <= RLvl)
        {
            baseIncrease *= RecipeTable.QualityModifier / 100f;
            PluginLog.LogDebug($"Boosted Increase: {baseIncrease}");
        }
        baseIncrease = MathF.Floor(baseIncrease);
        PluginLog.LogDebug($"Adj. Increase: {baseIncrease}");

        var qualityGain = (int)(baseIncrease * efficiency * conditionModifier * buffModifier);
        PluginLog.LogDebug($"Quality Gain: {qualityGain}");
        return qualityGain;
    }

    public void IncreaseProgressRaw(int progressGain)
    {
        Progress += progressGain;

        if (HasEffect(Effect.FinalAppraisal) && Progress >= MaxProgress)
        {
            Progress = MaxProgress - 1;
            RemoveEffect(Effect.FinalAppraisal);
        }
    }

    public void IncreaseQualityRaw(int qualityGain)
    {
        Quality += qualityGain;

        if (Stats.Level >= 11)
            StrengthenEffect(Effect.InnerQuiet);
    }

    public void IncreaseProgress(float efficiency) =>
        IncreaseProgressRaw(CalculateProgressGain(efficiency));

    public void IncreaseQuality(float efficiency) =>
        IncreaseQualityRaw(CalculateQualityGain(efficiency));
}
