using Craftimizer.Plugin;
using Lumina.Excel.GeneratedSheets;

namespace Craftimizer.Simulator;

public enum EffectType
{
    InnerQuiet,
    WasteNot,
    Veneration,
    GreatStrides,
    Innovation,
    FinalAppraisal,
    WasteNot2,
    MuscleMemory,
    Manipulation,
    HeartAndSoul,
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
}
