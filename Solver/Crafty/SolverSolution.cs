using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver.Crafty;

public readonly record struct SolverSolution(List<ActionType> Actions, SimulationState State);
