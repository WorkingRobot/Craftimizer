using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using DotNext.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Craftimizer.Utils;

internal sealed class SimulatedMacro
{
    public readonly record struct Reliablity
    {
        public sealed class Param
        {
            private List<int> DataList { get; }
            private ImGuiUtils.ViolinData? ViolinData { get; set; }

            public int Max { get; private set; }
            public int Min { get; private set; }
            public float Median { get; private set; }
            public float Average { get; private set; }

            public Param()
            {
                DataList = [];
            }

            public void Add(int value)
            {
                DataList.Add(value);
            }

            public void FinalizeData()
            {
                if (DataList.Count == 0)
                {
                    Average = Median = Max = Min = 0;
                    return;
                }

                Max = DataList.Max();
                Min = DataList.Min();
                if (DataList.Count % 2 == 0)
                    Median = (float)DataList.Order().Skip(DataList.Count / 2 - 1).Take(2).Average();
                else
                    Median = DataList.Order().ElementAt(DataList.Count / 2);
                Average = (float)DataList.Average();
            }

            public ImGuiUtils.ViolinData? GetViolinData(float barMax, int resolution, double bandwidth) =>
                ViolinData ??=
                    Min != Max ?
                        new(DataList, 0, barMax, resolution, bandwidth) :
                        null;
        }

        public readonly Param Progress = new();
        public readonly Param Quality = new();

        // Param is either collectability, quality, or hq%, depending on the recipe
        public readonly Param ParamScore = new();

        public Reliablity(in SimulationState startState, IEnumerable<ActionType> actions, int iterCount, RecipeData recipeData)
        {
            Func<SimulationState, int> getParam;
            if (recipeData.IsCollectable)
                getParam = s => s.Collectability;
            else if (recipeData.Recipe.RequiredQuality > 0)
            {
                var reqQual = recipeData.Recipe.RequiredQuality;
                getParam = s => (int)((float)s.Quality / reqQual * 100);
            }
            else if (recipeData.RecipeInfo.MaxQuality > 0)
                getParam = s => s.HQPercent;
            else
                getParam = s => 0;

            for (var i = 0; i < iterCount; ++i)
            {
                var sim = new RotationSimulator();
                var (_, state, _) = sim.ExecuteMultiple(startState, actions);
                Progress.Add(state.Progress);
                Quality.Add(state.Quality);
                ParamScore.Add(getParam(state));
            }
            Progress.FinalizeData();
            Quality.FinalizeData();
            ParamScore.FinalizeData();
        }
    }

    private sealed record Step
    {
        public ActionType Action { get; }
        public bool IsEphemeral { get; }
        // State *after* executing the action
        public ActionResponse Response { get; private set; }
        public SimulationState State { get; private set; }
        private Reliablity? Reliability { get; set; }

        public Step(ActionType action, RotationSimulator sim, in SimulationState lastState, out SimulationState newState)
        {
            Action = action;
            newState = Recalculate(sim, lastState);
        }

        // Call recalculate after this please!
        public Step(ActionType action, bool isEphemeral = false)
        {
            Action = action;
            IsEphemeral = isEphemeral;
        }

        public SimulationState Recalculate(RotationSimulator sim, in SimulationState lastState)
        {
            (Response, State) = sim.Execute(lastState, Action);
            Reliability = null;
            return State;
        }

        public Reliablity GetReliability(in SimulationState initialState, IEnumerable<ActionType> actionSet, RecipeData recipeData) =>
            Reliability ??=
                new(initialState, actionSet, Service.Configuration.ReliabilitySimulationCount, recipeData);
    };

    private List<Step> Macro { get; set; } = [];

    private SimulationState initialState;
    public SimulationState InitialState
    {
        get => initialState;
        set
        {
            if (initialState != value)
            {
                initialState = value;
                RecalculateState();
            }
        }
    }
    private object QueueLock { get; } = new();
    private List<Step> QueuedSteps { get; set; } = [];
    private List<Step> QueuedEphemeralSteps { get; set; } = [];

    public SimulationState FirstState => Macro.Count > 0 ? Macro[0].State : InitialState;
    public SimulationState State => Macro.Count > 0 ? Macro[^1].State : InitialState;

    public IEnumerable<ActionType> Actions => Macro.Select(m => m.Action);
    public int Count => Macro.Count;

    public (ActionType Action, ActionResponse Response, SimulationState State) this[int i]
    {
        get
        {
            var step = Macro[i];
            return (step.Action, step.Response, step.State);
        }
    }

    public Reliablity GetReliability(RecipeData recipeData, Index? idx = null) =>
        Macro.Count > 0 ?
            Macro[idx ?? ^1].GetReliability(InitialState, Actions.ToArray(), recipeData) :
            new(InitialState, Array.Empty<ActionType>(), 0, recipeData);

    private void TryRecalculateFrom(int index)
    {
        if (index < 0 || index >= Macro.Count)
            return;

        var state = index == 0 ? InitialState : Macro[index - 1].State;
        var sim = CreateSim();
        for (var i = index; i < Macro.Count; i++)
            state = Macro[i].Recalculate(sim, state);
    }

    public void RecalculateState() =>
        TryRecalculateFrom(0);

    public void RemoveRange(int index, int count) =>
        Macro.RemoveRange(index, count);

    public void Clear() =>
        Macro.Clear();

    public void Add(ActionType action)
    {
        Macro.Add(new(action, CreateSim(), State, out _));
    }

    public void Insert(int index, ActionType action)
    {
        if (index < 0 || index >= Macro.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Macro.Insert(index, new(action));
        TryRecalculateFrom(index);
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Macro.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Macro.RemoveAt(index);
        TryRecalculateFrom(index);
    }

    public void Move(int fromIdx, int toIdx)
    {
        var step = Macro[fromIdx];
        Macro.RemoveAt(fromIdx);
        Macro.Insert(toIdx, step);

        TryRecalculateFrom(Math.Min(fromIdx, toIdx));
    }

    public void RemoveEphemeral()
    {
        for (var i = Macro.Count - 1; i >= 0; --i)
        {
            if (Macro[i].IsEphemeral)
                Macro.RemoveAt(i);
        }
    }

    public int Enqueue(ActionType action, int? maxSize = null)
    {
        lock (QueueLock)
        {
            if (maxSize is { } size && QueuedSteps.Count + Macro.Count >= size)
                return size;

            QueuedEphemeralSteps.Clear();
            QueuedSteps.Add(new(action));
            return Macro.Count + QueuedSteps.Count;
        }
    }

    public int EnqueueEphemeral(IEnumerable<ActionType> actions, int? maxSize = null)
    {
        lock (QueueLock)
        {
            QueuedEphemeralSteps.Clear();
            foreach (var action in actions)
            {
                if (maxSize is { } size && QueuedSteps.Count + QueuedEphemeralSteps.Count + Macro.Count >= size)
                    return size;

                QueuedEphemeralSteps.Add(new(action, true));
            }
            return Macro.Count + QueuedSteps.Count + QueuedEphemeralSteps.Count;
        }
    }

    public void ClearQueue()
    {
        lock (QueueLock)
        {
            QueuedSteps.Clear();
            QueuedEphemeralSteps.Clear();
        }
    }

    public void FlushQueue()
    {
        lock (QueueLock)
        {
            if (QueuedSteps.Count > 0 || QueuedEphemeralSteps.Count > 0)
            {
                RemoveEphemeral();
                var startIdx = Macro.Count;

                Macro.AddRange(QueuedSteps);
                Macro.AddRange(QueuedEphemeralSteps);
                QueuedSteps.Clear();
                QueuedEphemeralSteps.Clear();
                TryRecalculateFrom(startIdx);
            }
        }
    }

    private static RotationSimulator CreateSim() =>
        Service.Configuration.ConditionRandomness ? new RotationSimulator() : new RotationSimulatorNoRandom();
}
