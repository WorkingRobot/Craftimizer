using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public sealed class SolverConcurrent : ISolver
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int arrayIdx, int subIdx)? EvalBestChild(ref SolverConfig config, int parentVisits, ref NodeScoresBuffer children) =>
        parentVisits == 0 ?
            null :
            SolverUtils.EvalBestChild<SolverConcurrent>(ref config, parentVisits, ref children);

    [Pure]
    public static Node Select(ref SolverConfig config, int rootNodeVisits, Node rootNode)
    {
        var node = rootNode;
        var nodeVisits = rootNodeVisits;
        while (true)
        {
            var expandable = !node.State.AvailableActions.IsEmpty;
            var likelyTerminal = node.Children.Count == 0;
            if (expandable || likelyTerminal)
                return node;

            // select the node with the highest score
            // if null (current node is invalid & not backpropagated just yet), try again from root
            var at = EvalBestChild(ref config, nodeVisits, ref node.ChildScores);
            if (at.HasValue)
            {
                nodeVisits = node.ChildScores.GetVisits(at.Value);
                node = node.ChildAt(at.Value);
            }
            else
            {
                node = rootNode;
                nodeVisits = rootNodeVisits;
            }
        }
    }

    public static (Node ExpandedNode, float Score)? ExpandAndRollout(ref SolverConfig config, float maxScore, Node rootNode, Random random, Simulator simulator, Node initialNode)
    {
        ref var initialState = ref initialNode.State;
        // expand once
        if (initialState.IsComplete)
            return (initialNode, initialState.CalculateScore(config.MaxStepCount) ?? 0);

        var poppedAction = initialState.AvailableActions.PopRandomConcurrent(random);
        if (!poppedAction.HasValue)
            return null;
        var expandedNode = initialNode.AddConcurrent(SolverUtils.Execute(simulator, initialState.State, poppedAction.Value, true));

        return SolverUtils.Rollout(ref config, maxScore, rootNode, expandedNode, random, simulator);
    }

    public static void Backpropagate(RootScores rootScores, Node rootNode, Node startNode, float score)
    {
        while (true)
        {
            if (startNode == rootNode)
            {
                rootScores.VisitConcurrent(score);
                break;
            }
            startNode.ParentScores!.Value.VisitConcurrent(startNode.ChildIdx, score);

            startNode = startNode.Parent!;
        }
    }

    public static bool SearchIter(ref SolverConfig config, RootScores rootScores, Node rootNode, Random random, Simulator simulator)
    {
        var selectedNode = Select(ref config, rootScores.Visits, rootNode);
        var rolledOut = ExpandAndRollout(ref config, rootScores.MaxScore, rootNode, random, simulator, selectedNode);
        if (!rolledOut.HasValue)
            return false;

        var (endNode, score) = rolledOut.Value;
        Backpropagate(rootScores, rootNode, endNode, score);
        return true;
    }

    public static void SearchThread(SolverConfig config, RootScores rootScores, Node rootNode, CancellationToken token) =>
        SolverUtils.Search<SolverConcurrent>(ref config, config.Iterations / config.ThreadCount, rootScores, rootNode, token);

    public static void Search(ref SolverConfig config, RootScores rootScores, Node rootNode, CancellationToken token)
    {
        var configP = config;
        var tasks = new Task[config.ThreadCount];
        for (var i = 0; i < config.ThreadCount; ++i)
            tasks[i] = Task.Run(() => SearchThread(configP, rootScores, rootNode, token), token);
        Task.WaitAll(tasks, CancellationToken.None);
    }
}
