using Craftimizer.Plugin;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
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
    public abstract float Efficiency { get; }
    public virtual float SuccessRate => 1f;
    public virtual int DurabilityCost => 10;

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

        if (Simulation.RollSuccess(SuccessRate))
            UseSuccess();

        if (Simulation.HasEffect(Effect.Manipulation))
            Simulation.RestoreDurability(5);
    }

    public abstract void UseSuccess();
}
