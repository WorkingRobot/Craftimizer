using Craftimizer.Simulator.Actions;
using Craftimizer.Simulator;

namespace Craftimizer.Solver.Algorithms;

public interface IAlgorithm
{
    abstract static SolverSolution Search(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token);
}
