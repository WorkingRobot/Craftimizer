using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

public interface ISolver
{
    abstract static bool SearchIter(ref SolverConfig config, RootScores rootScores, Node rootNode, Random random, Simulator simulator);

    abstract static void Search(ref SolverConfig config, RootScores rootScores, Node rootNode, CancellationToken token);
}
