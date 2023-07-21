using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Node = Craftimizer.Solver.Crafty.ArenaNode<Craftimizer.Solver.Crafty.SimulationNode>;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public sealed class Solver
{
    private SolverConfig config;
    private Node rootNode;
    private RootScores rootScores;

    public float MaxScore => rootScores.MaxScore;

    public Solver(SolverConfig config, SimulationState state)
    {
        this.config = config;
        var sim = new Simulator(state, config.MaxStepCount);
        rootNode = new(new(
            state,
            null,
            sim.CompletionState,
            sim.AvailableActionsHeuristic(config.StrictActions)
        ));
        rootScores = new();
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

    private static Node ExecuteActions(Simulator simulator, Node startNode, ReadOnlySpan<ActionType> actions, bool strict)
    {
        foreach (var action in actions)
        {
            var state = startNode.State;
            if (state.IsComplete)
                return startNode;

            if (!state.AvailableActions.HasAction(action))
                return startNode;
            state.AvailableActions.RemoveAction(action);

            startNode = startNode.Add(Execute(simulator, state.State, action, strict));
        }

        return startNode;
    }

    [Pure]
    private SolverSolution Solution()
    {
        var actions = new List<ActionType>();
        var node = rootNode;

        while (node.Children.Count != 0)
        {
            node = node.ChildAt(ChildMaxScore(ref node.ChildScores))!;

            if (node.State.Action != null)
                actions.Add(node.State.Action.Value);
        }

        //var at = node.ChildIdx;
        //ref var sum = ref node.ParentScores!.Value.Data[at.arrayIdx].ScoreSum.Span[at.subIdx];
        //ref var max = ref node.ParentScores!.Value.Data[at.arrayIdx].MaxScore.Span[at.subIdx];
        //ref var visits = ref node.ParentScores!.Value.Data[at.arrayIdx].Visits.Span[at.subIdx];
        //Console.WriteLine($"{sum} {max} {visits}");

        return new(actions, node.State.State);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int arrayIdx, int subIdx) ChildMaxScore(ref NodeScoresBuffer scores)
    {
        var length = scores.Count;
        var vecLength = Vector<float>.Count;

        var max = (0, 0);
        var maxScore = 0f;
        for (var i = 0; length > 0; ++i)
        {
            var iterCount = Math.Min(vecLength, length);

            ref var chunk = ref scores.Data[i];
            var m = new Vector<float>(chunk.MaxScore.Span);

            var idx = Intrinsics.HMaxIndex(m, iterCount);

            if (m[idx] >= maxScore)
            {
                max = (i, idx);
                maxScore = m[idx];
            }

            length -= iterCount;
        }

        return max;
    }

    // Calculates the best child node to explore next
    // Exploitation: ((1 - w) * (s / v)) + (w * m)
    // Exploration: sqrt(c * ln(V) / v)
    // w = maxScoreWeightingConstant
    // s = score sum
    // m = max score
    // v = visits
    // V = parentVisits
    // c = explorationConstant

    // Somewhat based off of https://en.wikipedia.org/wiki/Monte_Carlo_tree_search#Exploration_and_exploitation
    // Here, w_i = (1-w)*score sum
    // n_i = visits
    // max score is tacked onto it
    // N_i = parent visits
    // c = exploration constant (but crafty places it inside the sqrt..?)
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private (int arrayIdx, int subIdx) EvalBestChild(int parentVisits, ref NodeScoresBuffer scores)
    {
        var length = scores.Count;
        var vecLength = Vector<float>.Count;

        var C = MathF.Sqrt(config.ExplorationConstant * MathF.Log(parentVisits));
        var w = config.MaxScoreWeightingConstant;
        var W = 1f - w;
        var CVector = new Vector<float>(C);

        var max = (0, 0);
        var maxScore = 0f;
        for (var i = 0; length > 0; ++i)
        {
            var iterCount = Math.Min(vecLength, length);

            ref var chunk = ref scores.Data[i];
            var s = new Vector<float>(chunk.ScoreSum.Span);
            var vInt = new Vector<int>(chunk.Visits.Span);
            var m = new Vector<float>(chunk.MaxScore.Span);

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

        return max;
    }

    [Pure]
    public Node Select()
    {
        var node = rootNode;
        var nodeVisits = rootScores.Visits;

        while (true)
        {
            var expandable = !node.State.AvailableActions.IsEmpty;
            var likelyTerminal = node.Children.Count == 0;
            if (expandable || likelyTerminal)
                return node;

            // select the node with the highest score
            var at = EvalBestChild(nodeVisits, ref node.ChildScores);
            nodeVisits = node.ChildScores.GetVisits(at);
            node = node.ChildAt(at)!;
        }
    }

    public (Node ExpandedNode, float Score) ExpandAndRollout(Random random, Simulator simulator, Node initialNode)
    {
        ref var initialState = ref initialNode.State;
        // expand once
        if (initialState.IsComplete)
            return (initialNode, initialState.CalculateScore(config) ?? 0);

        var poppedAction = initialState.AvailableActions.PopRandom(random);
        var expandedNode = initialNode.Add(Execute(simulator, initialState.State, poppedAction, true));

        // playout to a terminal state
        var currentState = expandedNode.State.State;
        var currentCompletionState = expandedNode.State.SimulationCompletionState;
        var currentActions = expandedNode.State.AvailableActions;


        byte actionCount = 0;
        Span<ActionType> actions = stackalloc ActionType[Math.Min(config.MaxStepCount - currentState.ActionCount, config.MaxRolloutStepCount)];
        while (SimulationNode.GetCompletionState(currentCompletionState, currentActions) == CompletionState.Incomplete &&
            actionCount < actions.Length)
        {
            var nextAction = currentActions.SelectRandom(random);
            actions[actionCount++] = nextAction;
            (_, currentState) = simulator.Execute(currentState, nextAction);
            currentCompletionState = simulator.CompletionState;
            if (currentCompletionState != CompletionState.Incomplete)
                break;
            currentActions = simulator.AvailableActionsHeuristic(true);
        }

        // store the result if a max score was reached
        var score = SimulationNode.CalculateScoreForState(currentState, currentCompletionState, config) ?? 0;
        if (currentCompletionState == CompletionState.ProgressComplete)
        {
            if (score >= config.ScoreStorageThreshold && score >= MaxScore)
            {
                var terminalNode = ExecuteActions(simulator, expandedNode, actions[..actionCount], true);
                return (terminalNode, score);
            }
        }
        return (expandedNode, score);
    }

    public void Backpropagate(Node startNode, float score)
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

    private void ShowAllNodes()
    {
        static void ShowNodes(StringBuilder b, Node node, Stack<Node> path)
        {
            path.Push(node);
            b.AppendLine($"{new string(' ', path.Count)}{node.State.Action}");
            {
                for (var i = 0; i < node.Children.Count; ++i)
                {
                    var n = node.ChildAt((i >> 3, i & 7))!;
                    ShowNodes(b, n, path);
                }
                path.Pop();
            }
        }
        var b = new StringBuilder();
        ShowNodes(b, rootNode, new());
        Console.WriteLine(b.ToString());
    }

    private bool AllNodesComplete()
    {
        static bool NodesIncomplete(Node node, Stack<Node> path)
        {
            path.Push(node);
            if (node.Children.Count == 0)
            {
                if (!node.State.AvailableActions.IsEmpty)
                    return true;
            }
            else
            {
                for (var i = 0; i < node.Children.Count; ++i)
                {
                    var n = node.ChildAt((i >> 3, i & 7))!;
                    if (NodesIncomplete(n, path))
                        return true;
                }
                path.Pop();
            }
            return false;
        }
        return !NodesIncomplete(rootNode, new());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Search(int iterations, CancellationToken token)
    {
        Simulator simulator = new(rootNode.State.State, config.MaxStepCount);
        var random = rootNode.State.State.Input.Random;
        var n = 0;
        for (var i = 0; i < iterations || MaxScore == 0; i++)
        {
            if (token.IsCancellationRequested)
                break;

            var selectedNode = Select();
            var (endNode, score) = ExpandAndRollout(random, simulator, selectedNode);
            if (MaxScore == 0)
            {
                if (endNode == selectedNode)
                {
                    if (n++ > 5000)
                    {
                        n = 0;
                        if (AllNodesComplete())
                        {
                            //Console.WriteLine("All nodes solved for. Can't find a valid solution.");
                            //ShowAllNodes();
                            return;
                        }
                    }
                }
                else
                    n = 0;
            }

            Backpropagate(endNode, score);
        }
    }

    public static SolverSolution SearchStepwiseFurcated(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        SearchStepwiseFurcated(config, new SimulationState(input), actionCallback, token);

    public static SolverSolution SearchStepwiseFurcated(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
    {
        var definiteActionCount = 0;
        var bestSims = new List<(float Score, SolverSolution Result)>();

        var sim = new Simulator(state, config.MaxStepCount);

        var activeStates = new List<SolverSolution>() { new(new(), state) };

        while (activeStates.Count != 0)
        {
            if (token.IsCancellationRequested)
                break;

            var s = Stopwatch.StartNew();
            var tasks = new Task<(float MaxScore, int FurcatedActionIdx, SolverSolution Solution)>[config.ForkCount];
            for (var i = 0; i < config.ForkCount; i++)
            {
                var stateIdx = (int)((float)i / config.ForkCount * activeStates.Count);
                var st = activeStates[stateIdx];
                tasks[i] = Task.Run(() =>
                    {
                        var solver = new Solver(config, activeStates[stateIdx].State);
                        solver.Search(config.Iterations / config.ForkCount, token);
                        return (solver.MaxScore, stateIdx, solver.Solution());
                    }, token);
            }
            Task.WaitAll(tasks, token);
            s.Stop();

            if (token.IsCancellationRequested)
                break;

            var bestActions = tasks.Select(t => t.Result).OrderByDescending(r => r.MaxScore).Take(config.FurcatedActionCount).ToArray();

            var bestAction = bestActions[0];
            if (bestAction.MaxScore >= config.ScoreStorageThreshold)
            {
                var (maxScore, furcatedActionIdx, solution) = bestAction;
                var (activeActions, activeState) = activeStates[furcatedActionIdx];

                activeActions.AddRange(solution.Actions);
                return solution with { Actions = activeActions };
            }

            var newStates = new List<SolverSolution>(config.FurcatedActionCount);
            for (var i = 0; i < bestActions.Length; ++i)
            {
                var (maxScore, furcatedActionIdx, (solutionActions, solutionNode)) = bestActions[i];
                if (solutionActions.Count == 0)
                    continue;

                var (activeActions, activeState) = activeStates[furcatedActionIdx];

                var chosenAction = solutionActions[0];

                var newActions = new List<ActionType>(activeActions) { chosenAction };
                var newState = sim.Execute(activeState, chosenAction).NewState;
                if (sim.IsComplete)
                    bestSims.Add((maxScore, new(newActions, newState)));
                else
                    newStates.Add(new(newActions, newState));
            }

            if (bestSims.Count == 0 && newStates.Count != 0)
            {
                var definiteCount = definiteActionCount;
                var equalCount = int.MaxValue;
                var refActions = newStates[0].Actions;
                for (var i = 1; i < newStates.Count; ++i)
                {
                    var cmpActions = newStates[i].Actions;
                    var possibleCount = Math.Min(Math.Min(refActions.Count, cmpActions.Count), equalCount);
                    var completelyEqual = true;
                    for (var j = definiteCount; j < possibleCount; ++j)
                    {
                        if (refActions[j] != cmpActions[j])
                        {
                            equalCount = j;
                            completelyEqual = false;
                            break;
                        }
                    }
                    if (completelyEqual)
                        equalCount = possibleCount;
                }
                if (definiteCount != equalCount)
                {
                    for (var i = definiteCount; i < equalCount; ++i)
                        actionCallback?.Invoke(refActions[i]);

                    definiteActionCount = equalCount;
                }
            }

            activeStates = newStates;

            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");
        }

        if (bestSims.Count == 0)
            return new(new(), state);

        var result = bestSims.MaxBy(s => s.Score).Result;
        for (var i = definiteActionCount; i < result.Actions.Count; ++i)
            actionCallback?.Invoke(result.Actions[i]);

        return result;
    }

    public static SolverSolution SearchStepwiseForked(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        SearchStepwiseForked(config, new SimulationState(input), actionCallback, token);

    public static SolverSolution SearchStepwiseForked(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
    {
        var actions = new List<ActionType>();
        var sim = new Simulator(state, config.MaxStepCount);
        while (true)
        {
            if (token.IsCancellationRequested)
                break;

            if (sim.IsComplete)
                break;


            var s = Stopwatch.StartNew();
            var tasks = new Task<(float MaxScore, SolverSolution Solution)>[config.ForkCount];
            for (var i = 0; i < config.ForkCount; ++i)
                tasks[i] = Task.Run(() =>
                {
                    var solver = new Solver(config, state);
                    solver.Search(config.Iterations / config.ForkCount, token);
                    return (solver.MaxScore, solver.Solution());
                }, token);
            Task.WaitAll(tasks, token);
            s.Stop();

            if (token.IsCancellationRequested)
                break;

            var (maxScore, solution) = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore);

            if (maxScore >= config.ScoreStorageThreshold)
            {
                actions.AddRange(solution.Actions);
                return solution with { Actions = actions };
            }

            var chosenAction = solution.Actions[0];
            actionCallback?.Invoke(chosenAction);
            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

            (_, state) = sim.Execute(state, chosenAction);
            actions.Add(chosenAction);
        }

        return new(actions, state);
    }

    public static SolverSolution SearchStepwise(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        SearchStepwise(config, new SimulationState(input), actionCallback, token);

    public static SolverSolution SearchStepwise(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
    {
        var actions = new List<ActionType>();
        var sim = new Simulator(state, config.MaxStepCount);
        while (true)
        {
            if (token.IsCancellationRequested)
                break;

            if (sim.IsComplete)
                break;

            var solver = new Solver(config, state);

            var s = Stopwatch.StartNew();
            solver.Search(config.Iterations, token);
            s.Stop();

            var solution = solver.Solution();

            if (solver.MaxScore >= config.ScoreStorageThreshold)
            {
                actions.AddRange(solution.Actions);
                return solution with { Actions = actions };
            }

            var chosenAction = solution.Actions[0];
            actionCallback?.Invoke(chosenAction);
            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / s.Elapsed.TotalSeconds / 1000:0.00} kI/s");

            (_, state) = sim.Execute(state, chosenAction);
            actions.Add(chosenAction);
        }

        return new(actions, state);
    }

    public static SolverSolution SearchOneshotForked(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        SearchOneshotForked(config, new SimulationState(input), actionCallback, token);

    public static SolverSolution SearchOneshotForked(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
    {
        var tasks = new Task<(float MaxScore, SolverSolution Solution)>[config.ForkCount];
        for (var i = 0; i < config.ForkCount; ++i)
            tasks[i] = Task.Run(() =>
            {
                var solver = new Solver(config, state);
                solver.Search(config.Iterations / config.ForkCount, token);
                return (solver.MaxScore, solver.Solution());
            }, token);
        Task.WaitAll(tasks, CancellationToken.None);

        var solution = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore).Solution;
        foreach (var action in solution.Actions)
            actionCallback?.Invoke(action);

        return solution;
    }

    public static SolverSolution SearchOneshot(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        SearchOneshot(config, new SimulationState(input), actionCallback, token);

    public static SolverSolution SearchOneshot(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
    {
        var solver = new Solver(config, state);
        solver.Search(config.Iterations, token);
        var solution = solver.Solution();
        foreach (var action in solution.Actions)
            actionCallback?.Invoke(action);

        return solution;
    }
}
