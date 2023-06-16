using Craftimizer.Plugin;
using Dalamud.Utility;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Craftimizer.Simulator.Actions;

public enum ActionType
{
    AdvancedTouch,
    BasicSynthesis,
    BasicTouch,
    ByregotsBlessing,
    CarefulObservation,
    CarefulSynthesis,
    DelicateSynthesis,
    FinalAppraisal,
    FocusedSynthesis,
    FocusedTouch,
    GreatStrides,
    Groundwork,
    HastyTouch,
    HeartAndSoul,
    Innovation,
    IntensiveSynthesis,
    Manipulation,
    MastersMend,
    MuscleMemory,
    Observe,
    PreciseTouch,
    PreparatoryTouch,
    PrudentSynthesis,
    PrudentTouch,
    RapidSynthesis,
    Reflect,
    StandardTouch,
    TrainedEye,
    TrainedFinesse,
    TricksOfTheTrade,
    Veneration,
    WasteNot,
    WasteNot2,
}

internal static class ActionUtils
{
    private static readonly BaseAction[] Actions;
    
    static ActionUtils()
    {
        var types = typeof(BaseAction).Assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(BaseAction)) && !t.IsAbstract);
        Actions = Enum.GetNames<ActionType>()
            .Select(a => types.First(t => t.Name == a))
            .Select(t => (Activator.CreateInstance(t) as BaseAction)!)
            .ToArray();
    }

    private static BaseAction Action(this ActionType me) => Actions[(int)me];

    public static BaseAction With(this ActionType me, SimulationState simulation)
    {
        BaseAction.TLSSimulation.Value = simulation;
        return Action(me);
    }

    public static int Level(this ActionType me) =>
        Action(me).Level;

    public static ActionCategory Category(this ActionType me) =>
        Action(me).Category;

    private static (CraftAction? CraftAction, Action? Action) GetActionRow(this ActionType me, ClassJob classJob)
    {
        var actionId = Action(me).ActionId;
        if (LuminaSheets.CraftActionSheet.GetRow(actionId) is CraftAction baseCraftAction)
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
        else if (LuminaSheets.ActionSheet.GetRow(actionId) is Action baseAction)
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

    public static string GetName(this ActionType me, ClassJob classJob)
    {
        var (craftAction, action) = GetActionRow(me, classJob);
        if (craftAction != null)
            return craftAction.Name.ToDalamudString().TextValue;
        else if (action != null)
            return action.Name.ToDalamudString().TextValue;
        return "Unknown";
    }

    public static TextureWrap GetIcon(this ActionType me, ClassJob classJob)
    {
        var (craftAction, action) = GetActionRow(me, classJob);
        if (craftAction != null)
            return Icons.GetIconFromId(craftAction.Icon);
        else if (action != null)
            return Icons.GetIconFromId(action.Icon);
        // Old "Steady Hand" action icon
        return Icons.GetIconFromId(1953);
    }
}
