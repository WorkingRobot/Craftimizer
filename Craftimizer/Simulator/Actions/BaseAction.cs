namespace Craftimizer.Simulator.Actions;

internal abstract class BaseAction
{
    protected Simulation Simulation { get; }

    public BaseAction(Simulation simulation)
    {
        Simulation = simulation;
    }

    public abstract ActionCategory Category { get; }
    public abstract int Level { get; }

    public abstract int CPCost { get; }
    public abstract float Efficiency { get; }
    public virtual float SuccessRate => 1f;
    public virtual int DurabilityCost => 10;

    public virtual bool CanUse =>
        Simulation.Stats.Level >= Level && Simulation.CP >= CPCost;

    public virtual void Use()
    {
        Simulation.ReduceCP(CPCost);
        Simulation.ReduceDurability(DurabilityCost);
        if (Simulation.RollSuccess(SuccessRate))
            UseSuccess();
    }

    public abstract void UseSuccess();

}
