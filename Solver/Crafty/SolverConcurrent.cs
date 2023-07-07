using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public sealed class SolverConcurrent : ISolver
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void LoadChildData(Span<float> scoreSums, Span<int> visits, Span<float> maxScores, ref Node[] chunk, int iterCount)
    {
        for (var j = 0; j < iterCount; ++j)
        {
            var node = chunk[j]?.State.Scores ?? new();
            scoreSums[j] = node.ScoreSum;
            visits[j] = node.Visits;
            maxScores[j] = node.MaxScore;
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Node? EvalBestChild(ref SolverConfig config, int parentVisits, ref Node.ChildBuffer children) =>
        parentVisits == 0 ?
            null :
            SolverUtils.EvalBestChild<SolverConcurrent>(ref config, parentVisits, ref children);

    [Pure]
    public static Node Select(ref SolverConfig config, Node rootNode)
    {
        var node = rootNode;
        while (true)
        {
            var expandable = !node.State.AvailableActions.IsEmpty;
            var likelyTerminal = node.Children.Count == 0;
            if (expandable || likelyTerminal)
                return node;

            // select the node with the highest score
            // if null (current node is invalid & not backpropagated just yet), try again from root
            node = EvalBestChild(ref config, node.State.Scores.Visits, ref node.Children) ?? rootNode;
        }
    }

    public static (Node ExpandedNode, float Score)? ExpandAndRollout(ref SolverConfig config, Node rootNode, Random random, Simulator simulator, Node initialNode)
    {
        ref var initialState = ref initialNode.State;
        // expand once
        if (initialState.IsComplete)
            return (initialNode, initialState.CalculateScore(config.MaxStepCount) ?? 0);

        var poppedAction = initialState.AvailableActions.PopRandomConcurrent(random);
        if (!poppedAction.HasValue)
            return null;
        var expandedNode = initialNode.AddConcurrent(SolverUtils.Execute(simulator, initialState.State, poppedAction.Value, true));

        return SolverUtils.Rollout(ref config, rootNode, expandedNode, random, simulator);
    }

    public static void Backpropagate(Node rootNode, Node startNode, float score)
    {
        while (true)
        {
            startNode.State.Scores.VisitConcurrent(score);

            if (startNode == rootNode)
                break;

            startNode = startNode.Parent!;
        }
    }

    public static bool SearchIter(ref SolverConfig config, Node rootNode, Random random, Simulator simulator)
    {
        var selectedNode = Select(ref config, rootNode);
        var rolledOut = ExpandAndRollout(ref config, rootNode, random, simulator, selectedNode);
        if (!rolledOut.HasValue)
            return false;

        var (endNode, score) = rolledOut.Value;
        Backpropagate(rootNode, endNode, score);
        return true;
    }

    public static void SearchThread(SolverConfig config, Node rootNode, CancellationToken token) =>
        SolverUtils.Search<SolverConcurrent>(ref config, config.Iterations / config.ThreadCount, rootNode, token);

    public static void Search(ref SolverConfig config, Node rootNode, CancellationToken token)
    {
        var configP = config;
        var tasks = new Task[config.ThreadCount];
        for (var i = 0; i < config.ThreadCount; ++i)
            tasks[i] = Task.Run(() => SearchThread(configP, rootNode, token), token);
        Task.WaitAll(tasks, CancellationToken.None);
    }
}
