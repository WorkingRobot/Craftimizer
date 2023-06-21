using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public class Solver
{
    public SolverConfig Config;
    public Simulator Simulator;
    public Node RootNode;

    public Random Random => Simulator.Input.Random;

    public Solver(SolverConfig config, SimulationState state, bool strict)
    {
        Config = config;
        Simulator = new(state, config.MaxStepCount);
        RootNode = new(new(
            state,
            null,
            Simulator.CompletionState,
            Simulator.AvailableActionsHeuristic(strict)
        ));
    }

    public Solver(SolverConfig config, SimulationInput input, bool strict) : this(config, new SimulationState(input), strict)
    {
    }

    private SimulationNode Execute(SimulationState state, ActionType action, bool strict)
    {
        (_, var newState) = Simulator.Execute(state, action);
        return new(
            newState,
            action,
            Simulator.CompletionState,
            Simulator.AvailableActionsHeuristic(strict)
        );
    }

    public (Node EndNode, CompletionState State) ExecuteActions(Node startNode, ReadOnlySpan<ActionType> actions, bool strict = false)
    {
        foreach (var action in actions)
        {
            var state = startNode.State;
            if (state.IsComplete)
                return (startNode, state.CompletionState);

            if (!state.AvailableActions.HasAction(action))
                return (startNode, CompletionState.InvalidAction);
            state.AvailableActions.RemoveAction(action);

            startNode = startNode.Add(Execute(state.State, action, strict));
        }

        return (startNode, startNode.State.CompletionState);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T RustMaxBy<T>(ReadOnlySpan<T> source, Func<T, float> into)
    {
        var max = 0;
        var maxV = into(source[0]);
        for (var i = 1; i < source.Length; ++i)
        {
            var nextV = into(source[i]);
            if (maxV <= nextV)
            {
                max = i;
                maxV = nextV;
            }
        }
        return source[max];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> EvalBestChildVectorized(float w, float W, Vector<float> C, Vector<float> scoreSums, Vector<float> visits, Vector<float> maxScores)
    {
        var exploitation = W * (scoreSums / visits) + w * maxScores;
        var exploration = Vector.SquareRoot(C / visits);
        return exploitation + exploration;
    }

    private static int AlignToVectorLength(int length) =>
        (length + (Vector<float>.Count - 1)) & ~(Vector<float>.Count - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Node EvalBestChild(float parentVisits, ReadOnlySpan<Node> children)
    {
        var length = children.Length;

        var C = Config.ExplorationConstant * MathF.Log(parentVisits);
        var w = Config.MaxScoreWeightingConstant;
        var W = 1f - w;
        var CVector = new Vector<float>(C);

        Span<float> scoreSums = stackalloc float[Vector<float>.Count];
        Span<float> visits = stackalloc float[Vector<float>.Count];
        Span<float> maxScores = stackalloc float[Vector<float>.Count];

        var max = 0;
        var maxScore = 0f;
        for (var i = 0; i < length; i += Vector<float>.Count)
        {
            var iterCount = i + Vector<float>.Count > length ?
                length - i :
                Vector<float>.Count;

            for (var j = 0; j < iterCount; ++j)
            {
                var node = children[i + j].State.Scores;
                scoreSums[j] = node.ScoreSum;
                visits[j] = node.Visits;
                maxScores[j] = node.MaxScore;
            }
            var evalScores = EvalBestChildVectorized(w, W, CVector, new(scoreSums), new(visits), new(maxScores));

            for (var j = 0; j < iterCount; ++j)
            {
                if (evalScores[j] >= maxScore)
                {
                    max = i + j;
                    maxScore = evalScores[j];
                }
            }
        }

        return children[max];
    }

    public Node Select(Node selectedNode)
    {
        while (true)
        {
            var expandable = selectedNode.State.AvailableActions.Count != 0;
            var likelyTerminal = selectedNode.Children.Count == 0;
            if (expandable || likelyTerminal)
                return selectedNode;

            // select the node with the highest score
            selectedNode = EvalBestChild(selectedNode.State.Scores.Visits, CollectionsMarshal.AsSpan(selectedNode.Children));
        }
    }

    public (Node ExpandedNode, CompletionState State, float Score) ExpandAndRollout(Node initialNode)
    {
        ref var initialState = ref initialNode.State;
        // expand once
        if (initialState.IsComplete)
            return (initialNode, initialState.CompletionState, initialState.CalculateScore(Config.MaxStepCount) ?? 0);

        var randomAction = initialState.AvailableActions.SelectRandom(Random);
        initialState.AvailableActions.RemoveAction(randomAction);
        var expandedNode = initialNode.Add(Execute(initialState.State, randomAction, true));

        // playout to a terminal state
        var currentState = expandedNode.State.State;
        var currentCompletionState = expandedNode.State.SimulationCompletionState;
        var currentActions = expandedNode.State.AvailableActions;

        byte actionCount = 0;
        Span<ActionType> actions = stackalloc ActionType[Config.MaxStepCount];
        while (true)
        {
            if (SimulationNode.GetCompletionState(currentCompletionState, currentActions) != CompletionState.Incomplete)
                break;
            randomAction = currentActions.SelectRandom(Random);
            actions[actionCount++] = randomAction;
            (_, currentState) = Simulator.Execute(currentState, randomAction);
            currentCompletionState = Simulator.CompletionState;
            currentActions = Simulator.AvailableActionsHeuristic(true);
        }

        // store the result if a max score was reached
        var score = SimulationNode.CalculateScoreForState(currentState, currentCompletionState, Config.MaxStepCount) ?? 0;
        if (currentCompletionState == CompletionState.ProgressComplete)
        {
            if (score >= Config.ScoreStorageThreshold && score >= RootNode.State.Scores.MaxScore)
            {
                (var terminalNode, _) = ExecuteActions(expandedNode, actions[..actionCount], true);
                return (terminalNode, currentCompletionState, score);
            }
        }
        return (expandedNode, currentCompletionState, score);
    }

    public static void Backpropagate(Node startNode, Node targetNode, float score)
    {
        while (true)
        {
            startNode.State.Scores.Visit(score);

            if (startNode == targetNode)
                break;

            startNode = startNode.Parent!;
        }
    }

    public void Search(Node startNode)
    {
        for (var i = 0; i < Config.Iterations; i++)
        {
            var selectedNode = Select(startNode);
            var (endNode, _, score) = ExpandAndRollout(selectedNode);

            Backpropagate(endNode, startNode, score);
        }
    }

    public (List<ActionType> Actions, SimulationNode Node) Solution()
    {
        var actions = new List<ActionType>();
        var node = RootNode;
        while (node.Children.Count != 0)
        {
            node = RustMaxBy<Node>(CollectionsMarshal.AsSpan(node.Children), n => n.State.Scores.MaxScore);
            if (node.State.Action != null)
                actions.Add(node.State.Action.Value);
        }

        return (actions, node.State);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchStepwise(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback)
    {
        var state = new SimulationState(input);
        var actions = new List<ActionType>();
        var solver = new Solver(config, state, true);
        while (!solver.Simulator.IsComplete)
        {
            solver.Search(solver.RootNode);
            var (solution_actions, solution_node) = solver.Solution();

            if (solution_node.Scores.MaxScore >= 1.0)
            {
                actions.AddRange(solution_actions);
                return (actions, solution_node.State);
            }

            var chosen_action = solution_actions[0];
            (_, state) = solver.Simulator.Execute(state, chosen_action);
            actions.Add(chosen_action);

            actionCallback?.Invoke(chosen_action);

            solver = new Solver(config, state, true);
        }

        return (actions, state);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchOneshot(SolverConfig config, SimulationInput input)
    {
        var solver = new Solver(config, input, false);
        solver.Search(solver.RootNode);
        var (solution_actions, solution_node) = solver.Solution();
        return (solution_actions, solution_node.State);
    }
}
