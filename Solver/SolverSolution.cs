using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver;

public readonly record struct SolverSolution(List<ActionType> Actions, SimulationState State);
