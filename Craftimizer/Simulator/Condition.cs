using Craftimizer.Plugin;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Craftimizer.Simulator;

public enum Condition : ushort
{
    Poor = 0x0008,
    Normal = 0x0001,
    Good = 0x0002,
    Excellent = 0x0004,

    Centered = 0x0010,
    Sturdy = 0x0020,
    Pliant = 0x0040,
    Malleable = 0x0080,
    Primed = 0x0100,
    GoodOmen = 0x0200,
}

internal static class ConditionUtils
{
    public static Condition[] GetPossibleConditions(ushort conditionsFlag) =>
        Enum.GetValues<Condition>().Where(c => ((Condition)conditionsFlag).HasFlag(c)).ToArray();

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
