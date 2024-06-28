using Craftimizer.Simulator.Actions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Craftimizer.Simulator;

[StructLayout(LayoutKind.Auto)]
public record struct ActionStates
{
    public byte TouchComboIdx;
    public byte CarefulObservationCount;
    public bool UsedHeartAndSoul;
    public bool UsedQuickInnovation;
    public bool UsedTrainedPerfection;
    public bool ObserveCombo;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MutateState(BaseAction baseAction)
    {
        if (baseAction is BasicTouch)
            TouchComboIdx = 1;
        else if (TouchComboIdx == 1 && baseAction is StandardTouch)
            TouchComboIdx = 2;
        else
            TouchComboIdx = 0;

        if (baseAction is CarefulObservation)
            CarefulObservationCount++;

        if (baseAction is HeartAndSoul)
            UsedHeartAndSoul = true;

        if (baseAction is QuickInnovation)
            UsedQuickInnovation = true;

        if (baseAction is TrainedPerfection)
            UsedTrainedPerfection = true;

        ObserveCombo = baseAction is Observe;
    }
}
