using Craftimizer.Plugin;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Text;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Craftimizer.Simulator.Actions;

public abstract class BaseAction
{
    public static readonly Type[] Actions = typeof(BaseAction).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(BaseAction)) && !type.IsAbstract).ToArray();

    protected Simulation Simulation { get; }

    public BaseAction(Simulation simulation)
    {
        Simulation = simulation;
    }

    public abstract ActionCategory Category { get; }
    public abstract int Level { get; }
    // Doesn't matter from which class, we'll use the sheet to extrapolate the rest
    public abstract int ActionId { get; }

    public abstract int CPCost { get; }
    public virtual float Efficiency => 0f;
    public virtual bool IncreasesProgress => false;
    public virtual bool IncreasesQuality => false;
    public virtual float SuccessRate => 1f;
    public virtual int DurabilityCost => 10;
    public virtual bool IncreasesStepCount => true;

    private (CraftAction? CraftAction, Action? Action) GetActionRow(ClassJob classJob)
    {
        if (LuminaSheets.CraftActionSheet.GetRow((uint)ActionId) is CraftAction baseCraftAction)
        {
            return (classJob switch
            {
                ClassJob.Carpenter => baseCraftAction.CRP.Value!,
                ClassJob.Blacksmith => baseCraftAction.BSM.Value!,
                ClassJob.Armorer => baseCraftAction.ARM.Value!,
                ClassJob.Goldsmith => baseCraftAction.GSM.Value!,
                ClassJob.Leatherworker => baseCraftAction.LTW.Value!,
                ClassJob.Weaver => baseCraftAction.WVR.Value!,
                ClassJob.Alchemist => baseCraftAction.ALC.Value!,
                ClassJob.Culinarian => baseCraftAction.CUL.Value!,
                _ => baseCraftAction
            }, null);
        }
        else if (LuminaSheets.ActionSheet.GetRow((uint)ActionId) is Action baseAction)
        {
            return (null,
                LuminaSheets.ActionSheet.First(r =>
                r.Icon == baseAction.Icon &&
                r.ActionCategory.Row == baseAction.ActionCategory.Row &&
                r.Name.RawString == baseAction.Name.RawString &&
                (r.ClassJobCategory.Value?.IsClassJob(classJob) ?? false)
            ));
        }
        return (null, null);
    }

    public string GetName(ClassJob classJob)
    {
        var (craftAction, action) = GetActionRow(classJob);
        if (craftAction != null)
            return craftAction.Name;
        else if (action != null)
            return action.Name;
        return "Unknown";
    }

    public TextureWrap GetIcon(ClassJob classJob)
    {
        var (craftAction, action) = GetActionRow(classJob);
        if (craftAction != null)
            return Icons.GetIconFromId(craftAction.Icon);
        else if (action != null)
            return Icons.GetIconFromId(action.Icon);
        // Old "Steady Hand" action icon
        return Icons.GetIconFromId(1953);
    }

    public virtual bool CanUse =>
        Simulation.Stats.Level >= Level && Simulation.CP >= CPCost;

    public virtual void Use()
    {
        Simulation.ReduceCP(CPCost);
        Simulation.ReduceDurability(DurabilityCost);

        if (Simulation.HasEffect(Effect.Manipulation))
            Simulation.RestoreDurability(5);

        if (Simulation.RollSuccess(SuccessRate))
            UseSuccess();

        if (IncreasesStepCount)
            Simulation.IncreaseStepCount();
    }

    public virtual void UseSuccess()
    {
        if (Efficiency != 0f)
        {
            if (IncreasesProgress)
                Simulation.IncreaseProgress(Efficiency);
            if (IncreasesQuality)
                Simulation.IncreaseQuality(Efficiency);
        }
    }

    public virtual string Tooltip
    {
        get
        {
            var builder = new StringBuilder();
            builder.AppendLine(GetName(ClassJob.Carpenter));
            if (!CanUse)
                builder.AppendLine($"Cannot Use");
            builder.AppendLine($"Level {Level}");
            builder.AppendLine($"CP Cost: {CPCost}");
            if (DurabilityCost != 0)
                builder.AppendLine($"Durability Cost: {DurabilityCost}");
            if (IncreasesProgress)
                builder.AppendLine($"+{Simulation.CalculateProgressGain(Efficiency)} Progress");
            if (IncreasesQuality)
                builder.AppendLine($"+{Simulation.CalculateQualityGain(Efficiency)} Quality");
            if (!IncreasesStepCount)
                builder.AppendLine($"Does Not Increase Step Count");
            if (SuccessRate != 1f)
                builder.AppendLine($"{SuccessRate * 100}%% Success Rate");
            return builder.ToString();
        }
    }
}
