using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public class Solver
{
    public Simulator Simulator;
    public Arena<SimulationNode> Tree;

    //public Random Random => Simulator.Input.Random;

    public const int Iterations = 100000;
    public const float ScoreStorageThreshold = 1f;
    public const float MaxScoreWeightingConstant = 0.1f;
    public const float ExplorationConstant = 4f;
    public const int MaxStepCount = 25;

    public static void Write(string data)
    {
        if (false)
            Console.Write(data);
    }
    public static void WriteLine(string data)
    {
        if (false)
            Console.WriteLine(data);
    }

    public Solver(SimulationState state, bool strict)
    {
        Simulator = new(state);
        Tree = new(new()
        {
            State = state,
            Action = null,
            SimulationCompletionState = Simulator.CompletionState,
            AvailableActions = Simulator.AvailableActionsHeuristic(strict),
            Scores = new()
        });
    }

    public Solver(SimulationInput input) : this(new(input), false)
    {
    }

    private SimulationNode Execute(SimulationState state, ActionType action, bool strict)
    {
        (_, var newState) = Simulator.Execute(state, action);
        return new()
        {
            State = newState,
            Action = action,
            SimulationCompletionState = Simulator.CompletionState,
            AvailableActions = Simulator.AvailableActionsHeuristic(strict),
            Scores = new()
        };
    }

    public (int Index, CompletionState State) ExecuteActions(int startIndex, List<ActionType> actions, bool strict = false)
    {
        var currentIndex = startIndex;
        foreach (var action in actions)
        {
            var node = Tree.Get(currentIndex).State;
            if (node.IsComplete)
                return (currentIndex, node.CompletionState);

            if (!node.AvailableActions.Remove(action))
                return (currentIndex, CompletionState.InvalidAction);

            currentIndex = Tree.Insert(currentIndex, Execute(node.State, action, strict));
        }

        var currentNode = Tree.Get(currentIndex).State;
        return (currentIndex, currentNode.CompletionState);
    }

    public static float Eval(NodeScores node, NodeScores parent)
    {
        var w = MaxScoreWeightingConstant;
        var c = ExplorationConstant;

        var visits = node.Visits;
        var average_score = node.ScoreSum / visits;

        var exploitation = ((1f - w) * average_score) + (w * node.MaxScore);
        var exploration = MathF.Sqrt(c * MathF.Log(parent.Visits) / visits);

        WriteLine($"a {node.ScoreSum} {node.MaxScore}");
        WriteLine($"b {exploitation} {exploration}");

        return exploitation + exploration;
    }

    private enum Ordering
    {
        Less,
        Equal,
        Greater
    }

    private static V? RustMaxBy<V, T>(List<V> source, Func<V, T> into)
    {
        static Func<V, V, Ordering> compare_into(Func<T, T, Ordering> compare, Func<V, T> into) =>
            (a, b) => compare(into(a), into(b));

        static Func<T, T, Ordering> compare(IComparer<T> comparer) =>
            (x, y) => comparer.Compare(x, y) switch
            {
                < 0 => Ordering.Less,
                0 => Ordering.Equal,
                > 0 => Ordering.Greater,
            };

        static Func<V, V, V> max_by_fold(Func<V, V, Ordering> compare) =>
            (x, y) => compare(x, y) switch
            {
                Ordering.Less or Ordering.Equal => y,
                Ordering.Greater => x,
                _ => x
            };

        static V? reduce(List<V> d, Func<V, V, V> f)
        {
            V? accum = default!;
            for (var i = 0; i < d.Count; ++i)
                accum = i == 0 ? d[i] : f(accum, d[i]);
            return accum;
        }

        var comparer = compare_into(compare(Comparer<T>.Default), into);
        return reduce(source, max_by_fold(comparer));
    }

    public int Select(int currentIndex)
    {
        var selectedIndex = currentIndex;
        while (true)
        {
            var selectedNode = Tree.Get(selectedIndex);

            var expandable = selectedNode.State.AvailableActions.Count != 0;
            var likelyTerminal = selectedNode.Children.Count == 0;
            WriteLine("select:");
            WriteLine($"{expandable} {likelyTerminal}".ToLower());
            if (expandable || likelyTerminal) {
                break;
            }

            // select the node with the highest score
            selectedIndex = RustMaxBy(selectedNode.Children, n => Eval(Tree.Get(n).State.Scores, selectedNode.State.Scores));
            WriteLine($"{selectedIndex}");
        }
        return selectedIndex;
    }

    public (int Index, CompletionState State, float Score) ExpandAndRollout(int initialIndex)
    {
        WriteLine("expand_and_rollout");
        WriteLine($"{initialIndex}");
        // expand once
        var initialNode = Tree.Get(initialIndex).State;
        if (initialNode.IsComplete)
        {
            WriteLine($"ret {initialIndex} {initialNode.CompletionState}");
            return (initialIndex, initialNode.CompletionState, initialNode.CalculateScore() ?? 0);
        }
        var randomAction = initialNode.AvailableActions.ElementAt(0);
        initialNode.AvailableActions.Remove(randomAction);
        WriteLine($"pick {randomAction.IntName()}");
        var expandedState = Execute(initialNode.State, randomAction, true);
        var expandedIndex = Tree.Insert(initialIndex, expandedState);
        WriteLine($"ins {expandedIndex}");

        // playout to a terminal state
        var currentState = Tree.Get(expandedIndex).State;
        var preCount = currentState.State.ActionCount;
        while (true)
        {
            if (currentState.IsComplete)
                break;
            randomAction = currentState.AvailableActions.ElementAt(0);
            currentState = Execute(currentState.State, randomAction, true);
        }

        // store the result if a max score was reached
        var score = currentState.CalculateScore() ?? 0;
        if (currentState.CompletionState == CompletionState.ProgressComplete)
        {
            WriteLine($"calc: {score:0.00000}");
            if (score >= ScoreStorageThreshold && score >= Tree.Get(0).State.Scores.MaxScore)
            {
                WriteLine("exp_a");
                foreach (var action in currentState.State.ActionHistory.Skip(preCount))
                    Write($">{action.IntName()}");
                WriteLine("");

                (var terminalIndex, _) = ExecuteActions(expandedIndex, currentState.State.ActionHistory.Skip(preCount).ToList(), true);
                return (terminalIndex, currentState.CompletionState, score);
            }
        }
        return (expandedIndex, currentState.CompletionState, score);
    }

    public void Backpropagate(int startIndex, int targetIndex, float score)
    {
        WriteLine($"back {startIndex}->{targetIndex} {score}");
        var currentIndex = startIndex;
        while (true)
        {
            var currentNode = Tree.Get(currentIndex);
            var currentScores = currentNode.State.Scores;
            currentScores.Visits++;
            currentScores.ScoreSum += score;
            currentScores.MaxScore = Math.Max(currentScores.MaxScore, score);
            WriteLine($"bak {currentIndex} {currentScores.Visits} {currentScores.ScoreSum} {currentScores.MaxScore}");

            if (currentIndex == targetIndex)
                break;

            currentIndex = currentNode.Parent!.Value;
        }
    }

    public void Search(int startIndex)
    {
        for (var i = 0; i < Iterations; i++)
        {
            WriteLine($"search {i}");
            var selectedIndex = Select(startIndex);
            var (endIndex, state, score) = ExpandAndRollout(selectedIndex);

            WriteLine($"backp {endIndex} {score}");
            Backpropagate(endIndex, startIndex, score);
        }
    }

    public (List<ActionType> Actions, SimulationNode Node) Solution()
    {
        WriteLine("sol");
        var actions = new List<ActionType>();
        var node = Tree.Get(0);
        while (node.Children.Count != 0) {
            var next_index = RustMaxBy(node.Children, n => Tree.Get(n).State.Scores.MaxScore);
            WriteLine($"next: {next_index}");
            node = Tree.Get(next_index);
            if (node.State.Action != null)
            {
                WriteLine($"act: {node.State.Action.Value.IntName()}");
                actions.Add(node.State.Action.Value);
            }
        }

        return (actions, node.State);
    }

    public static (SimulationState SimState, CompletionState State) Simulate(SimulationInput input, List<ActionType> actions)
    {
        var solver = new Solver(input);
        var (index, result) = solver.ExecuteActions(0, actions);
        return (solver.Tree.Get(index).State.State, result);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchStepwise(SimulationInput input, List<ActionType> actions, Action<ActionType>? actionCallback)
    {
        var (state, result) = Simulate(input, actions);
        if (result != CompletionState.Incomplete) {
            return (actions, state);
        }

        var solver = new Solver(state, true);
        while (!solver.Simulator.IsComplete)
        {
            solver.Search(0);
            var (solution_actions, solution_node) = solver.Solution();

            if (solution_node.Scores.MaxScore >= 1.0) {
                actions.AddRange(solution_actions);
                return (actions, solution_node.State);
            }

            var chosen_action = solution_actions[0];
            (_, state) = solver.Simulator.Execute(state, chosen_action);
            actions.Add(chosen_action);

            actionCallback?.Invoke(chosen_action);

            solver = new Solver(state, true);
        }

        return (actions, state);
    }

    public static (List<ActionType> Actions, SimulationState State) SearchOneshot(SimulationInput input, List<ActionType> actions)
    {
        var solver = new Solver(input);
        solver.Search(0);
        var (solution_actions, solution_node) = solver.Solution();
        actions.AddRange(solution_actions);
        return (actions, solution_node.State);
    }
}
