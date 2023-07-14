using Craftimizer.Simulator.Actions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Craftimizer.Simulator;

[StructLayout(LayoutKind.Auto)]
public struct ActionStates
{
    public byte TouchComboIdx;
    public byte CarefulObservationCount;
    public bool UsedHeartAndSoul;
    public bool Observed;

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

        Observed = baseAction is Observe;
    }

    public override readonly string ToString() =>
        $"ActionStates {{ TouchComboIdx = {TouchComboIdx}, CarefulObservationCount = {CarefulObservationCount}, UsedHeartAndSoul = {UsedHeartAndSoul}, Observed = {Observed} }}";
}
