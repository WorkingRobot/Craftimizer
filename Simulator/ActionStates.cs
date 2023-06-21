using Craftimizer.Simulator.Actions;
using System.Runtime.InteropServices;

namespace Craftimizer.Simulator;

[StructLayout(LayoutKind.Auto)]
public struct ActionStates
{
    public byte TouchComboIdx;
    public byte CarefulObservationCount;
    public bool UsedHeartAndSoul;
    public bool Observed;

    public void MutateState(ActionType action)
    {
        if (action == ActionType.BasicTouch)
            TouchComboIdx = 1;
        else if (TouchComboIdx == 1 && action == ActionType.StandardTouch)
            TouchComboIdx = 2;
        else
            TouchComboIdx = 0;

        if (action == ActionType.CarefulObservation)
            CarefulObservationCount++;

        if (action == ActionType.HeartAndSoul)
            UsedHeartAndSoul = true;

        Observed = action == ActionType.Observe;
    }
}
