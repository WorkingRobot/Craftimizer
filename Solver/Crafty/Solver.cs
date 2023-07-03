using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public class Solver
{
    public SolverConfig Config;
    public Node RootNode;

    public Random Random;

    public Solver(SolverConfig config, SimulationState state, bool strict)
    {
        Config = config;
        Simulator sim = new(state, config.MaxStepCount);
        RootNode = new(new(
            state,
            null,
            sim.CompletionState,
            sim.AvailableActionsHeuristic(strict)
        ));
        Random = state.Input.Random;
    }

    public Solver(SolverConfig config, SimulationInput input, bool strict) : this(config, new SimulationState(input), strict)
    {
    }

    private static SimulationNode Execute(Simulator simulator, SimulationState state, ActionType action, bool strict)
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
    private static Node ChildMaxScore(ReadOnlySpan<Node> children)
    {
        var length = children.Length;
        var vecLength = Vector<float>.Count;

        Span<float> scores = stackalloc float[vecLength];

        var max = 0;
        var maxScore = 0f;
        for (var i = 0; i < length; i += vecLength)
        {
            var iterCount = i + vecLength > length ?
                length - i :
                vecLength;

            for (var j = 0; j < iterCount; ++j)
                scores[j] = children[i + j].State.Scores.MaxScore;

            var idx = Intrinsics.HMaxIndex(new Vector<float>(scores), iterCount);

            if (scores[idx] >= maxScore)
            {
                max = i + idx;
                maxScore = scores[idx];
            }
        }

        return children[max];
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Node EvalBestChild(float parentVisits, ReadOnlySpan<Node> children)
    {
        var length = children.Length;
        var vecLength = Vector<float>.Count;

        var C = Config.ExplorationConstant * MathF.Log(parentVisits);
        var w = Config.MaxScoreWeightingConstant;
        var W = 1f - w;
        var CVector = new Vector<float>(C);

        Span<float> scoreSums = stackalloc float[vecLength];
        Span<float> visits = stackalloc float[vecLength];
        Span<float> maxScores = stackalloc float[vecLength];
        
        var max = 0;
        var maxScore = 0f;
        for (var i = 0; i < length; i += vecLength)
        {
            var iterCount = i + vecLength > length ?
                length - i :
                vecLength;

            for (var j = 0; j < iterCount; ++j)
            {
                var node = children[i + j].State.Scores;
                scoreSums[j] = node.ScoreSum;
                visits[j] = node.Visits;
                maxScores[j] = node.MaxScore;
            }

            var exploitation = (W * (new Vector<float>(scoreSums) / new Vector<float>(visits))) + (w * new Vector<float>(maxScores));
            var exploration = Vector.SquareRoot(CVector / new Vector<float>(visits));
            var evalScores = exploitation + exploration;

            var idx = Intrinsics.HMaxIndex(evalScores, iterCount);
            
            if (evalScores[idx] >= maxScore)
            {
                max = i + idx;
                maxScore = evalScores[idx];
            }
        }

        return children[max];
    }

    [Pure]
    public Node Select()
    {
        var node = RootNode;
        while (true)
        {
            var expandable = node.State.AvailableActions.Count != 0;
            var likelyTerminal = node.Children.Count == 0;
            if (expandable || likelyTerminal)
                return node;

            // select the node with the highest score
            node = EvalBestChild(node.State.Scores.Visits, CollectionsMarshal.AsSpan(node.Children));
        }
    }

    public (Node ExpandedNode, CompletionState State, float Score) ExpandAndRollout(Simulator simulator, Node initialNode)
    {
        ref var initialState = ref initialNode.State;
        // expand once
        if (initialState.IsComplete)
            return (initialNode, initialState.CompletionState, initialState.CalculateScore(Config.MaxStepCount) ?? 0);

        var randomAction = initialState.AvailableActions.SelectRandom(Random);
        initialState.AvailableActions.RemoveAction(randomAction);
        var expandedNode = initialNode.Add(Execute(simulator, initialState.State, randomAction, true));

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
            (_, currentState) = simulator.Execute(currentState, randomAction);
            currentCompletionState = simulator.CompletionState;
            currentActions = simulator.AvailableActionsHeuristic(true);
        }

        // store the result if a max score was reached
        var score = SimulationNode.CalculateScoreForState(currentState, currentCompletionState, Config.MaxStepCount) ?? 0;
        if (currentCompletionState == CompletionState.ProgressComplete)
        {
            if (score >= Config.ScoreStorageThreshold && score >= RootNode.State.Scores.MaxScore)
            {
                (var terminalNode, _) = ExecuteActions(simulator, expandedNode, actions[..actionCount], true);
                return (terminalNode, currentCompletionState, score);
            }
        }
        return (expandedNode, currentCompletionState, score);
    }

    public void Backpropagate(Node startNode, float score)
    {
        while (true)
        {
            startNode.State.Scores.Visit(score);

            if (startNode == RootNode)
                break;

            startNode = startNode.Parent!;
        }
    }

    public void Search(CancellationToken token)
    {
        Simulator simulator = new(RootNode.State.State, Config.MaxStepCount);
        for (var i = 0; i < Config.Iterations; i++)
        {
            if (token.IsCancellationRequested)
                break;

            var selectedNode = Select();
            var (endNode, _, score) = ExpandAndRollout(simulator, selectedNode);

            Backpropagate(endNode, score);
        }
    }

    [Pure]
    public (List<ActionType> Actions, SimulationNode Node) Solution()
    {
        var actions = new List<ActionType>();
        var node = RootNode;
        while (node.Children.Count != 0)
        {
            node = ChildMaxScore(CollectionsMarshal.AsSpan(node.Children));

            if (node.State.Action != null)
                actions.Add(node.State.Action.Value);
        }

        return (actions, node.State);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchStepwise(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback, CancellationToken token = default) =>
        SearchStepwise(config, new SimulationState(input), actionCallback, token);

    public static (List<ActionType> Actions, SimulationState State) SearchStepwise(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback, CancellationToken token = default)
    {
        var actions = new List<ActionType>();
        Simulator sim = new(state, config.MaxStepCount);
        var solver = new Solver(config, state, true);
        while (!sim.IsComplete)
        {
            if (token.IsCancellationRequested)
                break;

            solver.Search(token);
            var (solution_actions, solution_node) = solver.Solution();

            if (solution_node.Scores.MaxScore >= 1.0)
            {
                actions.AddRange(solution_actions);
                return (actions, solution_node.State);
            }

            var chosen_action = solution_actions[0];
            (_, state) = sim.Execute(state, chosen_action);
            actions.Add(chosen_action);

            actionCallback?.Invoke(chosen_action);

            solver = new Solver(config, state, true);
        }

        return (actions, state);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchOneshot(SolverConfig config, SimulationInput input, CancellationToken token = default) =>
        SearchOneshot(config, new SimulationState(input), token);

    public static (List<ActionType> Actions, SimulationState State) SearchOneshot(SolverConfig config, SimulationState state, CancellationToken token = default)
    {
        var solver = new Solver(config, state, false);
        solver.Search(token);
        var (solution_actions, solution_node) = solver.Solution();
        return (solution_actions, solution_node.State);
    }
}
