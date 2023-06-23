using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // https://stackoverflow.com/a/73439472
    private static Vector128<float> HMax(Vector256<float> v1)
    {
        var v2 = Avx.Permute(v1, 0b10110001);
        var v3 = Avx.Max(v1, v2);
        var v4 = Avx.Permute(v3, 0b00001010);
        var v5 = Avx.Max(v3, v4);
        var v6 = Avx.ExtractVector128(v5, 1);
        var v7 = Sse.Max(v5.GetLower(), v6);
        return v7;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HMaxIndexScalar(Vector<float> v, int len)
    {
        var m = 0;
        for (var i = 1; i < len; ++i)
        {
            if (v[i] >= v[m])
                m = i;
        }
        return m;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // https://stackoverflow.com/a/23592221
    public static int HMaxIndexAVX2(Vector<float> v, int len)
    {
        // Remove NaNs
        var vfilt = Avx.Blend(v.AsVector256(), Vector256<float>.Zero, (byte)~((1 << len) - 1));

        // Find max value and broadcast to all lanes
        var vmax128 = HMax(vfilt);
        var vmax = Vector256.Create(vmax128, vmax128);

        // Find the highest index with that value, respecting len
        var vcmp = Avx.CompareEqual(vfilt, vmax);
        var mask = unchecked((uint)Avx2.MoveMask(vcmp.AsByte()));

        var inverseIdx = BitOperations.LeadingZeroCount(mask << ((8 - len) << 2)) >> 2;

        return len - 1 - inverseIdx;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HMaxIndex(Vector<float> v, int len) =>
        Avx2.IsSupported ?
        HMaxIndexAVX2(v, len) :
        HMaxIndexScalar(v, len);

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

            var idx = HMaxIndex(evalScores, iterCount);
            
            if (evalScores[idx] >= maxScore)
            {
                max = i + idx;
                maxScore = evalScores[idx];
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

        var randomAction = initialState.AvailableActions.First();//.SelectRandom(Random);
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
            randomAction = currentActions.First();//.SelectRandom(Random);
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
