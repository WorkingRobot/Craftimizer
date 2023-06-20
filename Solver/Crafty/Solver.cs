using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Craftimizer.Solver.Crafty;

// https://github.com/alostsock/crafty/blob/cffbd0cad8bab3cef9f52a3e3d5da4f5e3781842/crafty/src/simulator.rs
public class Solver
{
    public Simulator Simulator;
    public Arena<SimulationNode> Tree;

    //public Random Random => Simulator.Input.Random;

    public const int Iterations = 30000;
    public const float ScoreStorageThreshold = 1f;
    public const float MaxScoreWeightingConstant = 0.1f;
    public const float ExplorationConstant = 4f;
    public const int MaxStepCount = 25;

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

            if (!node.AvailableActions.HasAction(action))
                return (currentIndex, CompletionState.InvalidAction);
            node.AvailableActions.RemoveAction(action);

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

        return exploitation + exploration;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RustMaxBy(List<int> source, Func<int, float> into)
    {
        var max = 0;
        var maxV = into(source[0]);
        for (var i = 1; i < source.Count; ++i)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // Requires a multiple of Vector<float>.Count
    private static float[] EvalBestChildMultiple(float parentVisits, float[] scoreSums, float[] visits, float[] maxScores)
    {
        var C = ExplorationConstant * MathF.Log(parentVisits);
        var w = MaxScoreWeightingConstant;
        var W = 1f - w;
        var CVector = new Vector<float>(C);

        var length = scoreSums.Length;
        var result = new float[length];

        for (var i = 0; i < length; i += Vector<float>.Count)
        {
            var scoreSumsVector = new Vector<float>(scoreSums, i);
            var visitsVector = new Vector<float>(visits, i);
            var maxScoresVector = new Vector<float>(maxScores, i);
            var evalVector = EvalBestChildVectorized(w, W, CVector, scoreSumsVector, visitsVector, maxScoresVector);
            evalVector.CopyTo(result, i);
        }

        return result;
    }

    private float[] EvalAllChildrenDbg(float parentVisits, List<int> children)
    {
        var length = children.Count;
        var alignedLength = (length + (Vector<float>.Count - 1)) & ~(Vector<float>.Count - 1);
        var scoreSums = new float[alignedLength];
        var visits = new float[alignedLength];
        var maxScores = new float[alignedLength];


        for (var i = 0; i < length; ++i)
        {
            var node = Tree.Get(children[i]).State.Scores;
            scoreSums[i] = node.ScoreSum;
            visits[i] = node.Visits;
            maxScores[i] = node.MaxScore;
        }

        return EvalBestChildMultiple(parentVisits, scoreSums, visits, maxScores);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int EvalBestChild(float parentVisits, List<int> children)
    {
        var length = children.Count;
        var alignedLength = (length + (Vector<float>.Count - 1)) & ~(Vector<float>.Count - 1);
        var scoreSums = new float[alignedLength];
        var visits = new float[alignedLength];
        var maxScores = new float[alignedLength];


        for (var i = 0; i < length; ++i)
        {
            var node = Tree.Get(children[i]).State.Scores;
            scoreSums[i] = node.ScoreSum;
            visits[i] = node.Visits;
            maxScores[i] = node.MaxScore;
        }

        var evalScores = EvalBestChildMultiple(parentVisits, scoreSums, visits, maxScores);
        var maxIdx = 0;
        var max = evalScores[0];
        for(var i = 1; i < length; ++i)
        {
            if (evalScores[i] >= max)
            {
                maxIdx = i;
                max = evalScores[i];
            }
        }
        return children[maxIdx];
    }

    private int EvalBestChildScalar(List<int> children, NodeScores parent)
    {
        Console.WriteLine(children.Count);
        var C = ExplorationConstant * MathF.Log(parent.Visits);
        var w = MaxScoreWeightingConstant;
        var W = 1f - w;

        var ret = -1;
        var maxV = float.MinValue;
        foreach (var childNode in children)
        {
            var child = Tree.Get(childNode).State.Scores;
            var exploitation = (W * (child.ScoreSum / child.Visits)) + (w * child.MaxScore);
            var exploration = MathF.Sqrt(C / child.Visits);
            var score = exploitation + exploration;
            if (score >= maxV)
            {
                ret = childNode;
                maxV = score;
            }
        }
        return ret;
    }

    public int Select(int selectedIndex)
    {
        while (true)
        {
            var selectedNode = Tree.Get(selectedIndex);

            var expandable = selectedNode.State.AvailableActions.Count != 0;
            var likelyTerminal = selectedNode.Children.Count == 0;
            if (expandable || likelyTerminal) {
                return selectedIndex;
            }

            // select the node with the highest score
            selectedIndex = EvalBestChild(selectedNode.State.Scores.Visits, selectedNode.Children);
        }
    }

    public (int Index, CompletionState State, float Score) ExpandAndRollout(int initialIndex)
    {
        // expand once
        var initialNode = Tree.Get(initialIndex).State;
        if (initialNode.IsComplete)
            return (initialIndex, initialNode.CompletionState, initialNode.CalculateScore() ?? 0);

        var randomAction = initialNode.AvailableActions.ElementAt(0);
        initialNode.AvailableActions.RemoveAction(randomAction);
        var expandedState = Execute(initialNode.State, randomAction, true);
        var expandedIndex = Tree.Insert(initialIndex, expandedState);

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
            if (score >= ScoreStorageThreshold && score >= Tree.Get(0).State.Scores.MaxScore)
            {
                (var terminalIndex, _) = ExecuteActions(expandedIndex, currentState.State.ActionHistory.Skip(preCount).ToList(), true);
                return (terminalIndex, currentState.CompletionState, score);
            }
        }
        return (expandedIndex, currentState.CompletionState, score);
    }

    public void Backpropagate(int startIndex, int targetIndex, float score)
    {
        var currentIndex = startIndex;
        while (true)
        {
            var currentNode = Tree.Get(currentIndex);
            var currentScores = currentNode.State.Scores;
            currentScores.Visits++;
            currentScores.ScoreSum += score;
            if (currentScores.MaxScore < score)
                currentScores.MaxScore = score;

            if (currentIndex == targetIndex)
                break;

            currentIndex = currentNode.Parent!.Value;
        }
    }

    public void Search(int startIndex)
    {
        for (var i = 0; i < Iterations; i++)
        {
            var selectedIndex = Select(startIndex);
            var (endIndex, _, score) = ExpandAndRollout(selectedIndex);

            Backpropagate(endIndex, startIndex, score);
        }
    }

    public (List<ActionType> Actions, SimulationNode Node) Solution()
    {
        var actions = new List<ActionType>();
        var node = Tree.Get(0);
        while (node.Children.Count != 0) {
            var next_index = RustMaxBy(node.Children, n => Tree.Get(n).State.Scores.MaxScore);
            node = Tree.Get(next_index);
            if (node.State.Action != null)
            {
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

        Debugger.Break();
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
        Debugger.Break();

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
