using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Craftimizer.Simulator;

[StructLayout(LayoutKind.Auto)]
public record struct Effects
{
    public byte InnerQuiet;
    public byte WasteNot;
    public byte Veneration;
    public byte GreatStrides;
    public byte Innovation;
    public byte FinalAppraisal;
    public byte WasteNot2;
    public byte MuscleMemory;
    public byte Manipulation;
    public bool Expedience;
    public bool TrainedPerfection;
    public bool HeartAndSoul;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetDuration(EffectType effect, byte duration)
    {
        switch (effect)
        {
            case EffectType.InnerQuiet:
                if (duration == 0)
                    InnerQuiet = 0;
                break;
            case EffectType.WasteNot:
                WasteNot = duration;
                break;
            case EffectType.Veneration:
                Veneration = duration;
                break;
            case EffectType.GreatStrides:
                GreatStrides = duration;
                break;
            case EffectType.Innovation:
                Innovation = duration;
                break;
            case EffectType.FinalAppraisal:
                FinalAppraisal = duration;
                break;
            case EffectType.WasteNot2:
                WasteNot2 = duration;
                break;
            case EffectType.MuscleMemory:
                MuscleMemory = duration;
                break;
            case EffectType.Manipulation:
                Manipulation = duration;
                break;
            case EffectType.Expedience:
                Expedience = duration != 0;
                break;
            case EffectType.TrainedPerfection:
                TrainedPerfection = duration != 0;
                break;
            case EffectType.HeartAndSoul:
                HeartAndSoul = duration != 0;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Strengthen(EffectType effect)
    {
        if (effect == EffectType.InnerQuiet && InnerQuiet < 10)
            InnerQuiet++;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly byte GetDuration(EffectType effect) =>
        effect switch
        {
            EffectType.InnerQuiet => (byte)(InnerQuiet != 0 ? 1 : 0),
            EffectType.WasteNot => WasteNot,
            EffectType.Veneration => Veneration,
            EffectType.GreatStrides => GreatStrides,
            EffectType.Innovation => Innovation,
            EffectType.FinalAppraisal => FinalAppraisal,
            EffectType.WasteNot2 => WasteNot2,
            EffectType.MuscleMemory => MuscleMemory,
            EffectType.Manipulation => Manipulation,
            EffectType.Expedience => (byte)(Expedience ? 1 : 0),
            EffectType.TrainedPerfection => (byte)(TrainedPerfection ? 1 : 0),
            EffectType.HeartAndSoul => (byte)(HeartAndSoul ? 1 : 0),
            _ => 0
        };

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIndefinite(EffectType effect) =>
        effect is EffectType.InnerQuiet or EffectType.HeartAndSoul;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly byte GetStrength(EffectType effect) =>
        effect == EffectType.InnerQuiet ? InnerQuiet :
        (byte)(HasEffect(effect) ? 1 : 0);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasEffect(EffectType effect) =>
        GetDuration(effect) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecrementDuration()
    {
        if (WasteNot > 0)
            WasteNot--;
        if (WasteNot2 > 0)
            WasteNot2--;
        if (Veneration > 0)
            Veneration--;
        if (GreatStrides > 0)
            GreatStrides--;
        if (Innovation > 0)
            Innovation--;
        if (FinalAppraisal > 0)
            FinalAppraisal--;
        if (MuscleMemory > 0)
            MuscleMemory--;
        if (Manipulation > 0)
            Manipulation--;

        Expedience = false;
        TrainedPerfection = false;
    }
}
