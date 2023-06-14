using Craftimizer.Simulator.Actions;
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

    public int MaxDurability => RecipeTable.Durability * Recipe.DurabilityFactor;
    public int MaxQuality => (int)RecipeTable.Quality * Recipe.QualityFactor;
    public int MaxProgress => RecipeTable.Difficulty * Recipe.DifficultyFactor;

    public int StepCount => ActionHistory.Count;
    public int Progress { get; private set; }
    public int Quality { get; private set; }
    public int Durability { get; private set; }
    public int CP { get; private set; }
    public Condition Condition { get; private set; }
    public List<(Effect effect, int strength, int stepsLeft)> ActiveEffects { get; } = new();
    public List<BaseAction> ActionHistory { get; } = new();

    public bool IsFirstStep => StepCount == 0;

    private Random Random { get; } = new();

    public Simulation(CharacterStats stats, Recipe recipe)
    {
        Stats = stats;
        Recipe = recipe;
        Progress = 0;
        Quality = 0;
        Durability = MaxDurability;
        CP = Stats.CP;
        Condition = Condition.Normal;
    }

    public CompletionReason? Execute(BaseAction action)
    {
        if (!action.CanUse)
            return null;

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
            return CompletionReason.ProgressComplete;
        if (Durability <= 0)
            return CompletionReason.NoMoreDurability;

        return null;
    }

    public CompletionReason? Execute<T>() where T : BaseAction =>
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

    public BaseAction? GetPreviousAction(int stepsBack = 1)
    {
        return StepCount < stepsBack ? null : ActionHistory[^stepsBack];
    }

    public bool RollSuccess(float successRate) =>
        successRate >= Random.NextSingle();

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

    public void IncreaseProgress(float efficiency)
    {
        if (HasEffect(Effect.MuscleMemory))
        {
            efficiency += 1.00f;
            RemoveEffect(Effect.MuscleMemory);
        }
        if (HasEffect(Effect.Veneration))
            efficiency += 0.50f;

        // https://github.com/NotRanged/NotRanged.github.io/blob/0f4aee074f969fb05aad34feaba605057c08ffd1/app/js/ffxivcraftmodel.js#L88
        var baseIncrease = Stats.Craftsmanship * 10 / RecipeTable.ProgressDivider + 2;
        if (Stats.CLvl <= RLvl)
            baseIncrease *= RecipeTable.ProgressModifier / 100;

        Progress += (int)(baseIncrease * efficiency);

        if (HasEffect(Effect.FinalAppraisal) && Progress >= MaxProgress)
        {
            Progress = MaxProgress - 1;
            RemoveEffect(Effect.FinalAppraisal);
        }
    }

    public void IncreaseQuality(float efficiency)
    {
        efficiency += (GetEffect(Effect.InnerQuiet)?.Strength ?? 0) * 0.10f;
        if (HasEffect(Effect.GreatStrides))
        {
            efficiency += 1.00f;
            RemoveEffect(Effect.GreatStrides);
        }
        if (HasEffect(Effect.Innovation))
            efficiency += 0.50f;

        var conditionModifier = Condition switch
        {
            Condition.Poor => 0.50f,
            Condition.Good => 1.50f, // 1.75f if relic tool
            Condition.Excellent => 4.00f,
            _ => 1.00f,
        };

        var baseIncrease = Stats.Craftsmanship * 10 / RecipeTable.ProgressDivider + 2;
        if (Stats.CLvl <= RLvl)
            baseIncrease *= RecipeTable.ProgressModifier / 100;

        Quality += (int)(baseIncrease * efficiency * conditionModifier);

        if (Stats.Level >= 11)
            StrengthenEffect(Effect.InnerQuiet);
    }
}
