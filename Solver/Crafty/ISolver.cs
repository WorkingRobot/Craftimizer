using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

public interface ISolver
{
    abstract static void LoadChildData(Span<float> scoreSums, Span<int> visits, Span<float> maxScores, ref Node[] chunk, int iterCount);

    abstract static bool SearchIter(ref SolverConfig config, Node rootNode, Random random, Simulator simulator);

    abstract static void Search(ref SolverConfig config, Node rootNode, CancellationToken token);
}
