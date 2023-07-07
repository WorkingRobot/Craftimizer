using Craftimizer.Simulator.Actions;
using Craftimizer.Simulator;
using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;
public static class SolverUtils
{
    public static SimulationNode Execute(Simulator simulator, SimulationState state, ActionType action, bool strict)
    {
        (_, var newState) = simulator.Execute(state, action);
        return new(
            newState,
            action,
            simulator.CompletionState,
            simulator.AvailableActionsHeuristic(strict)
        );
    }

    public static (Node EndNode, CompletionState State) ExecuteActions(Simulator simulator, Node startNode, ReadOnlySpan<ActionType> actions, bool strict = false)
    {
        foreach (var action in actions)
        {
            var state = startNode.State;
            if (state.IsComplete)
                return (startNode, state.CompletionState);

            if (!state.AvailableActions.HasAction(action))
                return (startNode, CompletionState.InvalidAction);
            state.AvailableActions.RemoveAction(action);

            startNode = startNode.Add(Execute(simulator, state.State, action, strict));
        }

        return (startNode, startNode.State.CompletionState);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Node ChildMaxScore(ref Node.ChildBuffer children)
    {
        var length = children.Count;
        var vecLength = Vector<float>.Count;

        Span<float> scores = stackalloc float[vecLength];

        var max = (0, 0);
        var maxScore = 0f;
        for (var i = 0; length > 0; ++i)
        {
            var iterCount = Math.Min(vecLength, length);

            ref var chunk = ref children.Data[i];
            for (var j = 0; j < iterCount; ++j)
                scores[j] = chunk[j].State.Scores.MaxScore;

            var idx = Intrinsics.HMaxIndex(new Vector<float>(scores), iterCount);

            if (scores[idx] >= maxScore)
            {
                max = (i, idx);
                maxScore = scores[idx];
            }

            length -= iterCount;
        }

        return children.Data[max.Item1][max.Item2];
    }

    [Pure]
    public static (List<ActionType> Actions, SimulationNode Node) Solution(Node node)
    {
        var actions = new List<ActionType>();
        while (node.Children.Count != 0)
        {
            node = ChildMaxScore(ref node.Children);

            if (node.State.Action != null)
                actions.Add(node.State.Action.Value);
        }

        return (actions, node.State);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Node EvalBestChild<S>(ref SolverConfig config, int parentVisits, ref Node.ChildBuffer children) where S : ISolver
    {
        var length = children.Count;
        var vecLength = Vector<float>.Count;

        var C = MathF.Sqrt(config.ExplorationConstant * MathF.Log(parentVisits));
        var w = config.MaxScoreWeightingConstant;
        var W = 1f - w;
        var CVector = new Vector<float>(C);

        Span<float> scoreSums = stackalloc float[vecLength];
        Span<int> visits = stackalloc int[vecLength];
        Span<float> maxScores = stackalloc float[vecLength];

        var max = (0, 0);
        var maxScore = 0f;
        for (var i = 0; length > 0; ++i)
        {
            var iterCount = Math.Min(vecLength, length);

            S.LoadChildData(scoreSums, visits, maxScores, ref children.Data[i], iterCount);

            var s = new Vector<float>(scoreSums);
            var m = new Vector<float>(maxScores);
            var vInt = new Vector<int>(visits);
            vInt = Vector.Max(vInt, Vector<int>.One);
            var v = Vector.ConvertToSingle(vInt);
            var exploitation = (W * (s / v)) + (w * m);
            var exploration = CVector * Intrinsics.ReciprocalSqrt(v);
            var evalScores = exploitation + exploration;

            var idx = Intrinsics.HMaxIndex(evalScores, iterCount);

            if (evalScores[idx] >= maxScore)
            {
                max = (i, idx);
                maxScore = evalScores[idx];
            }

            length -= iterCount;
        }

        return children.Data[max.Item1][max.Item2];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Node ExpandedNode, float Score) Rollout(ref SolverConfig config, Node rootNode, Node expandedNode, Random random, Simulator simulator)
    {
        // playout to a terminal state
        var currentState = expandedNode.State.State;
        var currentCompletionState = expandedNode.State.SimulationCompletionState;
        var currentActions = expandedNode.State.AvailableActions;

        byte actionCount = 0;
        Span<ActionType> actions = stackalloc ActionType[config.MaxStepCount - currentState.ActionCount];
        while (true)
        {
            if (SimulationNode.GetCompletionState(currentCompletionState, currentActions) != CompletionState.Incomplete)
                break;
            var nextAction = currentActions.SelectRandom(random);
            actions[actionCount++] = nextAction;
            (_, currentState) = simulator.Execute(currentState, nextAction);
            currentCompletionState = simulator.CompletionState;
            currentActions = simulator.AvailableActionsHeuristic(true);
        }

        // store the result if a max score was reached
        var score = SimulationNode.CalculateScoreForState(currentState, currentCompletionState, config.MaxStepCount) ?? 0;
        if (currentCompletionState == CompletionState.ProgressComplete)
        {
            if (score >= config.ScoreStorageThreshold && score >= rootNode.State.Scores.MaxScore)
            {
                (var terminalNode, _) = ExecuteActions(simulator, expandedNode, actions[..actionCount], true);
                return (terminalNode, score);
            }
        }
        return (expandedNode, score);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Search<S>(ref SolverConfig config, int iterations, Node rootNode, CancellationToken token) where S : ISolver
    {
        Simulator simulator = new(rootNode.State.State, config.MaxStepCount);
        var random = rootNode.State.State.Input.Random;
        for (var i = 0; i < iterations; i++)
        {
            if (token.IsCancellationRequested)
                break;

            if (!S.SearchIter(ref config, rootNode, random, simulator))
            {
                // Retry, count this iteration as moot
                i--;
                continue;
            }
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Node CreateRootNode(SolverConfig config, SimulationInput input, bool strict) =>
        CreateRootNode(config, new SimulationState(input), strict);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Node CreateRootNode(SolverConfig config, SimulationState state, bool strict)
    {
        var sim = new Simulator(state, config.MaxStepCount);
        return new(new(
            state,
            null,
            sim.CompletionState,
            sim.AvailableActionsHeuristic(strict)
        ));
    }

    public static (List<ActionType> Actions, SimulationState State) SearchStepwise<S>(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback, CancellationToken token = default) where S : ISolver =>
        SearchStepwise<S>(config, new SimulationState(input), actionCallback, token);

    public static (List<ActionType> Actions, SimulationState State) SearchStepwise<S>(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token = default) where S : ISolver
    {
        var actions = new List<ActionType>();
        var sim = new Simulator(state, config.MaxStepCount);
        var rootNode = CreateRootNode(config, state, true);
        while (!sim.IsComplete)
        {
            if (token.IsCancellationRequested)
                break;

            S.Search(ref config, rootNode, token);
            var (solution_actions, solution_node) = Solution(rootNode);

            if (solution_node.Scores.MaxScore >= 1.0)
            {
                actions.AddRange(solution_actions);
                return (actions, solution_node.State);
            }

            var chosen_action = solution_actions[0];
            (_, state) = sim.Execute(state, chosen_action);
            actions.Add(chosen_action);

            actionCallback?.Invoke(chosen_action);

            rootNode = CreateRootNode(config, state, true);
        }

        return (actions, state);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchOneshot<S>(SolverConfig config, SimulationInput input, CancellationToken token = default) where S : ISolver =>
        SearchOneshot<S>(config, new SimulationState(input), token);

    public static (List<ActionType> Actions, SimulationState State) SearchOneshot<S>(SolverConfig config, SimulationState state, CancellationToken token = default) where S : ISolver
    {
        var rootNode = CreateRootNode(config, state, false);
        S.Search(ref config, rootNode, token);
        var (solution_actions, solution_node) = Solution(rootNode);
        return (solution_actions, solution_node.State);
    }
}
