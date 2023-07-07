using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public sealed class SolverSingle : ISolver
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void LoadChildData(Span<float> scoreSums, Span<int> visits, Span<float> maxScores, ref Node[] chunk, int iterCount)
    {
        for (var j = 0; j < iterCount; ++j)
        {
            ref var node = ref chunk[j].State.Scores;
            scoreSums[j] = node.ScoreSum;
            visits[j] = node.Visits;
            maxScores[j] = node.MaxScore;
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Node EvalBestChild(ref SolverConfig config, int parentVisits, ref ArenaBuffer<Node> children) =>
        SolverUtils.EvalBestChild<SolverSingle>(ref config, parentVisits, ref children);

    [Pure]
    public static Node Select(ref SolverConfig config, Node node)
    {
        while (true)
        {
            var expandable = !node.State.AvailableActions.IsEmpty;
            var likelyTerminal = node.Children.Count == 0;
            if (expandable || likelyTerminal)
                return node;

            // select the node with the highest score
            node = EvalBestChild(ref config, node.State.Scores.Visits, ref node.Children);
        }
    }

    public static (Node ExpandedNode, float Score) ExpandAndRollout(ref SolverConfig config, Node rootNode, Random random, Simulator simulator, Node initialNode)
    {
        ref var initialState = ref initialNode.State;
        // expand once
        if (initialState.IsComplete)
            return (initialNode, initialState.CalculateScore(config.MaxStepCount) ?? 0);

        var poppedAction = initialState.AvailableActions.PopRandom(random);
        var expandedNode = initialNode.Add(SolverUtils.Execute(simulator, initialState.State, poppedAction, true));

        return SolverUtils.Rollout(ref config, rootNode, expandedNode, random, simulator);
    }

    public static void Backpropagate(Node rootNode, Node startNode, float score)
    {
        while (true)
        {
            startNode.State.Scores.Visit(score);

            if (startNode == rootNode)
                break;

            startNode = startNode.Parent!;
        }
    }

    public static bool SearchIter(ref SolverConfig config, Node rootNode, Random random, Simulator simulator)
    {
        var selectedNode = Select(ref config, rootNode);
        var (endNode, score) = ExpandAndRollout(ref config, rootNode, random, simulator, selectedNode);

        Backpropagate(rootNode, endNode, score);
        return true;
    }

    public static void Search(ref SolverConfig config, Node rootNode, CancellationToken token) =>
        SolverUtils.Search<SolverSingle>(ref config, config.Iterations, rootNode, token);
}
