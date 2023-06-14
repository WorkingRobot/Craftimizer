namespace Craftimizer.Simulator;

public enum ActionResponse
{
    SimulationComplete,
    ActionNotUnlocked,
    NotEnoughCP,
    NoDurability,
    CannotUseAction,

    UsedAction,
    ProgressComplete,
    NoMoreDurability,
}
