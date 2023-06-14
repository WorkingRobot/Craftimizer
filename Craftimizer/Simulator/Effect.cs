using Craftimizer.Plugin;
using Lumina.Excel.GeneratedSheets;

namespace Craftimizer.Simulator;

public enum Effect
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
}

internal static class EffectExtensions
{
    public static uint StatusId(this Effect me) =>
        me switch
        {
            Effect.InnerQuiet => 251,
            Effect.WasteNot => 252,
            Effect.Veneration => 2226,
            Effect.GreatStrides => 254,
            Effect.Innovation => 2189,
            Effect.FinalAppraisal => 2190,
            Effect.WasteNot2 => 257,
            Effect.MuscleMemory => 2191,
            Effect.Manipulation => 258,
            _ => 3412,
        };

    public static Status Status(this Effect me) =>
        LuminaSheets.StatusSheet.GetRow(me.StatusId())!;
}
