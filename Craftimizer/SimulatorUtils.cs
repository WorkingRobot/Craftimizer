using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Action = Lumina.Excel.Sheets.Action;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using ClassJob = Craftimizer.Simulator.ClassJob;
using Condition = Craftimizer.Simulator.Condition;
using Status = Lumina.Excel.Sheets.Status;
using Craftimizer.Utils;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Lumina.Text.Payloads;

namespace Craftimizer.Plugin;

internal static class ActionUtils
{
    private static readonly (CraftAction? CraftAction, Action? Action)[,] ActionRows;

    static ActionUtils()
    {
        var actionTypes = Enum.GetValues<ActionType>();
        var classJobs = Enum.GetValues<ClassJob>();
        ActionRows = new (CraftAction? CraftAction, Action? Action)[actionTypes.Length, classJobs.Length];
        foreach (var actionType in actionTypes)
        {
            var actionId = actionType.Base().ActionId;
            if (LuminaSheets.CraftActionSheet.TryGetRow(actionId, out var baseCraftAction ))
            {
                foreach (var classJob in classJobs)
                {
                    ActionRows[(int)actionType, (int)classJob] = (classJob switch
                    {
                        ClassJob.Carpenter => baseCraftAction.CRP.Value,
                        ClassJob.Blacksmith => baseCraftAction.BSM.Value,
                        ClassJob.Armorer => baseCraftAction.ARM.Value,
                        ClassJob.Goldsmith => baseCraftAction.GSM.Value,
                        ClassJob.Leatherworker => baseCraftAction.LTW.Value,
                        ClassJob.Weaver => baseCraftAction.WVR.Value,
                        ClassJob.Alchemist => baseCraftAction.ALC.Value,
                        ClassJob.Culinarian => baseCraftAction.CUL.Value,
                        _ => baseCraftAction
                    }, null);
                }
            }
            if (LuminaSheets.ActionSheet.TryGetRow(actionId, out var baseAction))
            {
                var possibleActions = LuminaSheets.ActionSheet.Where(r =>
                        r.Icon == baseAction.Icon &&
                        r.ActionCategory.RowId == baseAction.ActionCategory.RowId &&
                        r.Name.Equals(baseAction.Name));
                
                foreach (var classJob in classJobs)
                    ActionRows[(int)actionType, (int)classJob] = (null, possibleActions.First(r => r.ClassJobCategory.ValueNullable?.IsClassJob(classJob) ?? false));
            }
        }
    }

    public static void Initialize() { }

    public static (CraftAction? CraftAction, Action? Action) GetActionRow(this ActionType me, ClassJob classJob) =>
        ActionRows[(int)me, (int)classJob];

    public static uint GetId(this ActionType me, ClassJob classJob)
    {
        var (craftAction, action) = GetActionRow(me, classJob);
        return craftAction?.RowId ?? action?.RowId ?? 0;
    }

    public static string GetName(this ActionType me, ClassJob classJob)
    {
        var (craftAction, action) = GetActionRow(me, classJob);
        return (craftAction?.Name ?? action?.Name)?.AsSpan().ExtractText() ?? "Unknown";
    }

    public static ITextureIcon GetIcon(this ActionType me, ClassJob classJob)
    {
        var (craftAction, action) = GetActionRow(me, classJob);
        // 1953 = Old "Steady Hand" action icon
        return Service.IconManager.GetIconCached(craftAction?.Icon ?? action?.Icon ?? 1953);
    }

    public static ActionType? GetActionTypeFromId(uint actionId, ClassJob classJob, bool isCraftAction)
    {
        foreach (var action in Enum.GetValues<ActionType>())
        {
            var row = action.GetActionRow(classJob);
            if (isCraftAction)
            {
                if (row.CraftAction?.RowId == actionId)
                    return action;
            }
            else
            {
                if (row.Action?.RowId == actionId)
                    return action;
            }
        }
        return null;
    }
}

internal static class ClassJobUtils
{
    public static byte GetClassJobIndex(this ClassJob me) =>
        me switch
        {
            ClassJob.Carpenter => 8,
            ClassJob.Blacksmith => 9,
            ClassJob.Armorer => 10,
            ClassJob.Goldsmith => 11,
            ClassJob.Leatherworker => 12,
            ClassJob.Weaver => 13,
            ClassJob.Alchemist => 14,
            ClassJob.Culinarian => 15,
            _ => 0
        };

    public static ClassJob? GetClassJobFromIdx(byte classJobIdx) =>
        classJobIdx switch
        {
            8 => ClassJob.Carpenter,
            9 => ClassJob.Blacksmith,
            10 => ClassJob.Armorer,
            11 => ClassJob.Goldsmith,
            12 => ClassJob.Leatherworker,
            13 => ClassJob.Weaver,
            14 => ClassJob.Alchemist,
            15 => ClassJob.Culinarian,
            _ => null
        };

    public static sbyte GetExpArrayIdx(this ClassJob me) =>
        LuminaSheets.ClassJobSheet.GetRow(me.GetClassJobIndex())!.ExpArrayIndex;

    public static unsafe short GetPlayerLevel(this ClassJob me) =>
        PlayerState.Instance()->ClassJobLevels[me.GetExpArrayIdx()];

    public static unsafe bool CanPlayerUseManipulation(this ClassJob me) =>
        UIState.Instance()->IsUnlockLinkUnlockedOrQuestCompleted(ActionType.Manipulation.GetActionRow(me).Action!.Value.UnlockLink.RowId);

    public static string GetName(this ClassJob me)
    {
        var job = LuminaSheets.ClassJobSheet.GetRow(me.GetClassJobIndex());
        return job.Name.ExtractText().ToLowerInvariant();
    }

    public static string GetNameArticle(this ClassJob me)
    {
        var job = LuminaSheets.ClassJobSheet.GetRow(me.GetClassJobIndex());
        if (LuminaSheets.ClassJobSheet.Language == Lumina.Data.Language.English)
        {
            if (me is ClassJob.Alchemist or ClassJob.Armorer)
                return "an";
        }
        return "a";
    }

    public static string GetAbbreviation(this ClassJob me)
    {
        var job = LuminaSheets.ClassJobSheet.GetRow(me.GetClassJobIndex());
        return job.Abbreviation.ExtractText();
    }

    public static Quest GetUnlockQuest(this ClassJob me) =>
        LuminaSheets.QuestSheet.GetRow(65720 + (uint)me);

    public static ushort GetIconId(this ClassJob me) =>
        (ushort)(62000 + me.GetClassJobIndex());

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
    private static (uint Name, uint Description) AddonIds(this Condition me) =>
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

    private static Vector3 AddRGB(this Condition me) =>
        me switch
        {
            Condition.Poor => new(-50, -50, -50),
            Condition.Normal => new(32, 48, 64),
            Condition.Good => new(80, -80, 0),
            Condition.Excellent => Vector3.Zero, // All the other conditions are just a single lerp, this one is different
            Condition.Centered => new(200, 200, 0),
            Condition.Sturdy => new(-100, 45, 155),
            Condition.Pliant => new(0, 250, 0),
            Condition.Malleable => new(-80, -40, 180),
            Condition.Primed => new(30, -155, 200),
            Condition.GoodOmen => new(100, 20, 0),
            _ => Vector3.Zero // Unknown
        };

    private const float ConditionCyclePeriod = 19 / 30f;
    // The real period of all condition color cycles are 0.633... (19/30) seconds
    // Interp accepts 0-1
    public static Vector4 GetColor(this Condition me, float interp)
    {
        //var baseColor = new Vector3(0.85f, 0.85f, 0.85f); // Middle-ish pixels of synthesis2_hr1.tex's condition circle

        Vector3 addRgb;
        // Excellent has 6 lerps and 1 ending constant
        if (me == Condition.Excellent)
        {
            addRgb = interp switch
            {
                < 0.155f => Vector3.Lerp(new(128, 0, 0), new(128, 80, 0), (interp - 0) / 0.155f),
                < 0.315f => Vector3.Lerp(new(128, 80, 0), new(128, 128, 0), (interp - 0.155f) / 0.16f),
                < 0.475f => Vector3.Lerp(new(128, 128, 0), new(0, 64, 0), (interp - 0.315f) / 0.16f),
                < 0.630f => Vector3.Lerp(new(0, 64, 0), new(0, 128, 128), (interp - 0.475f) / 0.155f),
                < 0.790f => Vector3.Lerp(new(0, 128, 128), new(0, 0, 128), (interp - 0.630f) / 0.16f),
                < 0.945f => Vector3.Lerp(new(0, 0, 128), new(64, 0, 64), (interp - 0.790f) / 0.155f),
                _ => new(64, 0, 64)
            };
        }
        // Period is twice as fast so we oscillate at twice that speed
        else if (me == Condition.Malleable)
        {
            if (interp > .5f)
                interp -= .5f;
            if (interp > .25f)
                interp = .25f - (interp - .25f);
            interp *= 4;
            addRgb = Vector3.Lerp(new(-80, -40, 180), new(-41, -1, 254), interp);
        }
        else
        {
            if (interp > .5f)
                interp = .5f - (interp - .5f);
            interp *= 2;
            addRgb = me switch
            {
                Condition.Poor => Vector3.Lerp(new(-50, -50, -50), new(-1, -1, -1), interp),
                Condition.Normal => Vector3.Lerp(new(32, 48, 64), new(63, 95, 127), interp),
                Condition.Good => Vector3.Lerp(new(80, -80, 0), new(159, -1, 0), interp),
                Condition.Centered => Vector3.Lerp(new(199, 199, 0), new(100, 100, 0), interp),
                Condition.Sturdy => Vector3.Lerp(new(-100, 45, 155), new(-51, 89, 254), interp),
                Condition.Pliant => Vector3.Lerp(new(0, 150, 0), new(0, 249, 0), interp),
                Condition.Primed => Vector3.Lerp(new(-30, -255, 50), new(29, -156, 199), interp),
                Condition.GoodOmen => Vector3.Lerp(new(100, 20, 0), new(100, 99, 99), interp),
                _ => default
            };
        }

        return new(addRgb / 255, 1);
    }

    public static Vector4 GetColor(this Condition me, TimeSpan time)
    {
        return me.GetColor((float)(time.TotalSeconds % ConditionCyclePeriod / ConditionCyclePeriod));
    }

    public static string Name(this Condition me) =>
        LuminaSheets.AddonSheet.GetRow(me.AddonIds().Name).Text.ExtractText();

    public static string Description(this Condition me, bool isRelic)
    {
        var text = LuminaSheets.AddonSheet.GetRow(me.AddonIds().Description).Text;
        if (!text.Any(p => p is { Type: ReadOnlySePayloadType.Macro, MacroCode: MacroCode.Float }))
            return text.ExtractText();

        ReadOnlySeString finalText = new();
        foreach (var payload in text)
        {
            if (payload is { Type: ReadOnlySePayloadType.Macro, MacroCode: MacroCode.Float })
                finalText += new ReadOnlySePayload(ReadOnlySePayloadType.Text, default, Encoding.UTF8.GetBytes(isRelic ? "1.75" : "1.5"));
            else
                finalText += payload;
        }
        return finalText.ExtractText();
    }
}

internal static class EffectUtils
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
            EffectType.Manipulation => 1164,
            EffectType.HeartAndSoul => 2665,
            EffectType.Expedience => 3812,
            EffectType.TrainedPerfection => 3813,
            _ => throw new ArgumentOutOfRangeException(nameof(me)),
        };

    public static bool IsIndefinite(this EffectType me) =>
        me is EffectType.InnerQuiet or EffectType.HeartAndSoul or EffectType.TrainedPerfection;

    public static Status Status(this EffectType me) =>
        LuminaSheets.StatusSheet.GetRow(me.StatusId())!;

    public static ushort GetIconId(this EffectType me, int strength)
    {
        var status = me.Status();
        var iconId = status.Icon;
        if (status.MaxStacks != 0)
            iconId += (uint)Math.Clamp(strength, 1, status.MaxStacks) - 1;
        return (ushort)iconId;
    }

    public static ITextureIcon GetIcon(this EffectType me, int strength) =>
        Service.IconManager.GetIconCached(me.GetIconId(strength));

    public static string GetTooltip(this EffectType me, int strength, int duration)
    {
        var status = me.Status();
        var name = new StringBuilder();
        name.Append(status.Name.ExtractText());
        if (status.MaxStacks != 0)
            name.Append($" {strength}");
        if (!status.IsPermanent)
            name.Append($" > {duration}");
        return name.ToString();
    }
}
