using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
    private (List<ActionType> Actions, SimulationNode Node) Solution()
    {
        var actions = new List<ActionType>();
        var node = rootNode;

        while (node.Children.Count != 0)
        {
            node = node.ChildAt(ChildMaxScore(ref node.ChildScores))!;

            if (node.State.Action != null)
                actions.Add(node.State.Action.Value);
        }

        var at = node.ChildIdx;
        ref var sum = ref node.ParentScores!.Value.Data[at.arrayIdx].ScoreSum.Span[at.subIdx];
        ref var max = ref node.ParentScores!.Value.Data[at.arrayIdx].MaxScore.Span[at.subIdx];
        ref var visits = ref node.ParentScores!.Value.Data[at.arrayIdx].Visits.Span[at.subIdx];
        //Console.WriteLine($"{sum} {max} {visits}");

        return (actions, node.State);
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
            return (initialNode, initialState.CalculateScore(config.MaxStepCount) ?? 0);

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
        var score = SimulationNode.CalculateScoreForState(currentState, currentCompletionState, config.MaxStepCount) ?? 0;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Search(CancellationToken token, int iterations)
    {
        Simulator simulator = new(rootNode.State.State, config.MaxStepCount);
        var random = rootNode.State.State.Input.Random;
        for (var i = 0; i < iterations; i++)
        {
            if (token.IsCancellationRequested)
                break;

            var selectedNode = Select();
            var (endNode, score) = ExpandAndRollout(random, simulator, selectedNode);

            Backpropagate(endNode, score);
        }
    }

    public static (List<ActionType> Actions, SimulationState State) SearchStepwiseFurcated(SolverConfig config, SimulationInput input, CancellationToken token = default) =>
        SearchStepwiseFurcated(config, new SimulationState(input), token);

    public static (List<ActionType> Actions, SimulationState State) SearchStepwiseFurcated(SolverConfig config, SimulationState state, CancellationToken token = default)
    {
        var bestSims = new List<(float Score, (List<ActionType> Actions, SimulationState State) Result)>();

        var sim = new Simulator(state, config.MaxStepCount);

        var activeStates = new List<(List<ActionType> Actions, SimulationState State)>() { (new(), state) };

        while (activeStates.Count != 0)
        {
            if (token.IsCancellationRequested)
                break;

            var s = Stopwatch.StartNew();
            var tasks = new List<Task<(float MaxScore, int FurcatedActionIdx, (List<ActionType> Actions, SimulationNode Node) Solution)>>(config.ForkCount);
            for (var i = 0; i < config.ForkCount; i++)
            {
                var stateIdx = (int)((float)i / config.ForkCount * activeStates.Count);
                var st = activeStates[stateIdx];
                tasks.Add(
                    Task.Run(() =>
                    {
                        var solver = new Solver(config, activeStates[stateIdx].State);
                        solver.Search(token, config.Iterations / config.ForkCount);
                        return (solver.MaxScore, stateIdx, solver.Solution());
                    }, token)
                );
            }
            Task.WaitAll(tasks.ToArray(), CancellationToken.None);
            s.Stop();

            var bestActions = tasks.Select(t => t.Result).OrderByDescending(r => r.MaxScore).Take(config.FurcatedActionCount).ToArray();

            var bestAction = bestActions[0];
            if (bestAction.MaxScore >= config.ScoreStorageThreshold)
            {
                var (maxScore, furcatedActionIdx, (solutionActions, solutionNode)) = bestAction;
                var (activeActions, activeState) = activeStates[furcatedActionIdx];

                activeActions.AddRange(solutionActions);
                return (activeActions, solutionNode.State);
            }

            var newStates = new List<(List<ActionType> Actions, SimulationState State)>(config.FurcatedActionCount);
            for (var i = 0; i < bestActions.Length; ++i)
            {
                var (maxScore, furcatedActionIdx, (solutionActions, solutionNode)) = bestActions[i];
                var (activeActions, activeState) = activeStates[furcatedActionIdx];

                var chosenAction = solutionActions[0];

                var newActions = new List<ActionType>(activeActions) { chosenAction };
                var newState = sim.Execute(activeState, chosenAction).NewState;
                if (sim.IsComplete)
                    bestSims.Add((maxScore, (newActions, newState)));
                else
                    newStates.Add((newActions, newState));
            }

            activeStates = newStates;

            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");
        }

        return bestSims.MaxBy(s => s.Score).Result;
    }

    public static (List<ActionType> Actions, SimulationState State) SearchStepwiseForked(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        SearchStepwiseForked(config, new SimulationState(input), actionCallback, token);

    public static (List<ActionType> Actions, SimulationState State) SearchStepwiseForked(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
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
            var tasks = new Task<(float MaxScore, (List<ActionType> Actions, SimulationNode Node) Solution)>[config.ForkCount];
            for (var i = 0; i < config.ForkCount; ++i)
                tasks[i] = Task.Run(() =>
                {
                    var solver = new Solver(config, state);
                    solver.Search(token, config.Iterations / config.ForkCount);
                    return (solver.MaxScore, solver.Solution());
                }, token);
            Task.WaitAll(tasks, CancellationToken.None);
            s.Stop();

            var (maxScore, (solutionActions, solutionNode)) = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore);

            if (maxScore >= config.ScoreStorageThreshold)
            {
                actions.AddRange(solutionActions);
                return (actions, solutionNode.State);
            }

            var chosen_action = solutionActions[0];
            actionCallback?.Invoke(chosen_action);
            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / config.ForkCount / s.Elapsed.TotalSeconds / 1000:0.00} kI/s/t");

            (_, state) = sim.Execute(state, chosen_action);
            actions.Add(chosen_action);
        }

        return (actions, state);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchStepwise(SolverConfig config, SimulationInput input, Action<ActionType>? actionCallback = null, CancellationToken token = default) =>
        SearchStepwise(config, new SimulationState(input), actionCallback, token);

    public static (List<ActionType> Actions, SimulationState State) SearchStepwise(SolverConfig config, SimulationState state, Action<ActionType>? actionCallback = null, CancellationToken token = default)
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
            solver.Search(token, config.Iterations);
            s.Stop();

            var (solution_actions, solution_node) = solver.Solution();

            if (solver.MaxScore >= config.ScoreStorageThreshold)
            {
                actions.AddRange(solution_actions);
                return (actions, solution_node.State);
            }

            var chosen_action = solution_actions[0];
            actionCallback?.Invoke(chosen_action);
            Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}ms {config.Iterations / s.Elapsed.TotalSeconds / 1000:0.00} kI/s");

            (_, state) = sim.Execute(state, chosen_action);
            actions.Add(chosen_action);
        }

        return (actions, state);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchOneshotForked(SolverConfig config, SimulationInput input, CancellationToken token = default) =>
        SearchOneshotForked(config, new SimulationState(input), token);

    public static (List<ActionType> Actions, SimulationState State) SearchOneshotForked(SolverConfig config, SimulationState state, CancellationToken token = default)
    {
        var tasks = new Task<(float MaxScore, (List<ActionType> Actions, SimulationNode Node) Solution)>[config.ForkCount];
        for (var i = 0; i < config.ForkCount; ++i)
            tasks[i] = Task.Run(() =>
            {
                var solver = new Solver(config, state);
                solver.Search(token, config.Iterations / config.ForkCount);
                return (solver.MaxScore, solver.Solution());
            }, token);
        Task.WaitAll(tasks, CancellationToken.None);

        var (solutionActions, solutionNode) = tasks.Select(t => t.Result).MaxBy(r => r.MaxScore).Solution;
        return (solutionActions, solutionNode.State);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchOneshot(SolverConfig config, SimulationInput input, CancellationToken token = default) =>
        SearchOneshot(config, new SimulationState(input), token);

    public static (List<ActionType> Actions, SimulationState State) SearchOneshot(SolverConfig config, SimulationState state, CancellationToken token = default)
    {
        var solver = new Solver(config, state);
        solver.Search(token, config.Iterations);
        var (solution_actions, solution_node) = solver.Solution();
        return (solution_actions, solution_node.State);
    }
}
