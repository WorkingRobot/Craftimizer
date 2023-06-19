using ImGuiScene;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using Craftimizer.Simulator.Actions;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System;
using ClassJob = Craftimizer.Simulator.ClassJob;
using Condition = Craftimizer.Simulator.Condition;
using Craftimizer.Simulator;
using System.Text;
using System.Runtime.CompilerServices;

namespace Craftimizer.Plugin;

internal static class ActionUtils
{
    private static (CraftAction? CraftAction, Action? Action) GetActionRow(this ActionType me, ClassJob classJob)
    {
        var actionId = me.WithUnsafe().ActionId;
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
        if (action != null)
            return action.Name.ToDalamudString().TextValue;
        return "Unknown";
    }

    public static TextureWrap GetIcon(this ActionType me, ClassJob classJob)
    {
        var (craftAction, action) = GetActionRow(me, classJob);
        if (craftAction != null)
            return Icons.GetIconFromId(craftAction.Icon);
        if (action != null)
            return Icons.GetIconFromId(action.Icon);
        // Old "Steady Hand" action icon
        return Icons.GetIconFromId(1953);
    }
}

internal static class ClassJobExtensions
{
    public static bool IsClassJob(this ClassJobCategory me, ClassJob classJob) =>
        classJob switch
        {
            ClassJob.Carpenter => me.CRP,
            ClassJob.Blacksmith => me.BSM,
            ClassJob.Armorer => me.ARM,
            ClassJob.Goldsmith => me.GSM,
            ClassJob.Leatherworker => me.LTW,
            ClassJob.Weaver => me.WVR,
            ClassJob.Alchemist => me.ALC,
            ClassJob.Culinarian => me.CUL,
            _ => false
        };
}

internal static class ConditionUtils
{
    public static (uint Name, uint Description) AddonIds(this Condition me) =>
        me switch
        {
            Condition.Poor => (229, 14203),
            Condition.Normal => (226, 14200),
            Condition.Good => (227, 14201),
            Condition.Excellent => (228, 14202),
            Condition.Centered => (239, 14204),
            Condition.Sturdy => (240, 14205),
            Condition.Pliant => (241, 14206),
            Condition.Malleable => (13455, 14208),
            Condition.Primed => (13454, 14207),
            Condition.GoodOmen => (14214, 14215),
            _ => (226, 14200) // Unknown
        };

    public static string Name(this Condition me) =>
        LuminaSheets.AddonSheet.GetRow(me.AddonIds().Name)!.Text.ToDalamudString().TextValue;

    public static string Description(this Condition me, bool isRelic)
    {
        var text = LuminaSheets.AddonSheet.GetRow(me.AddonIds().Description)!.Text.ToDalamudString();
        for (var i = 0; i < text.Payloads.Count; ++i)
            if (text.Payloads[i] is RawPayload)
                text.Payloads[i] = new TextPayload(isRelic ? "1.75" : "1.5");
        return text.TextValue;
    }
}

internal static class EffectExtensions
{
    public static uint StatusId(this EffectType me) =>
        me switch
        {
            EffectType.InnerQuiet => 251,
            EffectType.WasteNot => 252,
            EffectType.Veneration => 2226,
            EffectType.GreatStrides => 254,
            EffectType.Innovation => 2189,
            EffectType.FinalAppraisal => 2190,
            EffectType.WasteNot2 => 257,
            EffectType.MuscleMemory => 2191,
            EffectType.Manipulation => 258,
            EffectType.HeartAndSoul => 2665,
            _ => 3412,
        };

    public static Status Status(this EffectType me) =>
        LuminaSheets.StatusSheet.GetRow(me.StatusId())!;

    public static ushort GetIconId(this EffectType me, int strength)
    {
        var status = me.Status();
        uint iconId = status.Icon;
        if (status.MaxStacks != 0)
            iconId += (uint)Math.Clamp(strength, 1, status.MaxStacks) - 1;
        return (ushort)iconId;
    }

    public static TextureWrap GetIcon(this EffectType me, int strength) =>
        Icons.GetIconFromId(me.GetIconId(strength));

    public static string GetTooltip(this EffectType me, int strength, int duration)
    {
        var status = me.Status();
        var name = new StringBuilder();
        name.Append(status.Name.ToDalamudString().TextValue);
        if (status.MaxStacks != 0)
            name.Append($" {strength}");
        if (!status.IsPermanent)
            name.Append($" > {duration}");
        return name.ToString();
    }
}
