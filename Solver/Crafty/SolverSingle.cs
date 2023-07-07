using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public sealed class SolverSingle : ISolver
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int arrayIdx, int subIdx) EvalBestChild(ref SolverConfig config, int parentVisits, ref NodeScoresBuffer children) =>
        SolverUtils.EvalBestChild<SolverSingle>(ref config, parentVisits, ref children);

    [Pure]
    public static Node Select(ref SolverConfig config, int nodeVisits, Node node)
    {
        while (true)
        {
            var expandable = !node.State.AvailableActions.IsEmpty;
            var likelyTerminal = node.Children.Count == 0;
            if (expandable || likelyTerminal)
                return node;

            // select the node with the highest score
            var at = EvalBestChild(ref config, nodeVisits, ref node.ChildScores);
            nodeVisits = node.ChildScores.GetVisits(at);
            node = node.ChildAt(at)!;
        }
    }

    public static (Node ExpandedNode, float Score) ExpandAndRollout(ref SolverConfig config, float maxScore, Node rootNode, Random random, Simulator simulator, Node initialNode)
    {
        ref var initialState = ref initialNode.State;
        // expand once
        if (initialState.IsComplete)
            return (initialNode, initialState.CalculateScore(config.MaxStepCount) ?? 0);

        var poppedAction = initialState.AvailableActions.PopRandom(random);
        var expandedNode = initialNode.Add(SolverUtils.Execute(simulator, initialState.State, poppedAction, true));

        return SolverUtils.Rollout(ref config, maxScore, rootNode, expandedNode, random, simulator);
    }

    public static void Backpropagate(RootScores rootScores, Node rootNode, Node startNode, float score)
    {
        while (true)
        {
            if (startNode == rootNode)
            {
                rootScores.Visit(score);
                break;
            }
            startNode.ParentScores!.Value.Visit(startNode.ChildIdx, score);

            startNode = startNode.Parent!;
        }
    }

    public static bool SearchIter(ref SolverConfig config, RootScores rootScores, Node rootNode, Random random, Simulator simulator)
    {
        var selectedNode = Select(ref config, rootScores.Visits, rootNode);
        var (endNode, score) = ExpandAndRollout(ref config, rootScores.MaxScore, rootNode, random, simulator, selectedNode);

        Backpropagate(rootScores, rootNode, endNode, score);
        return true;
    }

    public static void Search(ref SolverConfig config, RootScores rootScores, Node rootNode, CancellationToken token) =>
        SolverUtils.Search<SolverSingle>(ref config, config.Iterations, rootScores, rootNode, token);
}
