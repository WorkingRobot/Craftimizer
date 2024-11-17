using Craftimizer.Simulator.Actions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Craftimizer.Simulator;

[StructLayout(LayoutKind.Auto)]
public record struct ActionStates
{
    public ActionProc Combo;
    public byte CarefulObservationCount;
    public bool UsedHeartAndSoul;
    public bool UsedQuickInnovation;
    public bool UsedTrainedPerfection;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MutateState(BaseAction baseAction)
    {
        if (baseAction is BasicTouch)
            Combo = ActionProc.UsedBasicTouch;
        else if ((Combo == ActionProc.UsedBasicTouch && baseAction is StandardTouch) || baseAction is Observe)
            Combo = ActionProc.AdvancedTouch;
        else
            Combo = ActionProc.None;

        if (baseAction is CarefulObservation)
            CarefulObservationCount++;

        if (baseAction is HeartAndSoul)
            UsedHeartAndSoul = true;

        if (baseAction is QuickInnovation)
            UsedQuickInnovation = true;

        if (baseAction is TrainedPerfection)
            UsedTrainedPerfection = true;
    }
}
