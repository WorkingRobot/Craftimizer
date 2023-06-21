using Craftimizer.Simulator.Actions;

namespace Craftimizer.Simulator;

public record struct ActionStates
{
    public byte TouchComboIdx { get; set; }
    public byte CarefulObservationCount { get; set; }
    public bool UsedHeartAndSoul { get; set; }
    public bool Observed { get; set; }

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
