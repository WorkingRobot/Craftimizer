using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using Sim = Craftimizer.Simulator.Simulator;
using SimNoRandom = Craftimizer.Simulator.SimulatorNoRandom;

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
                DataList = new();
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
            if (recipeData.Recipe.ItemResult.Value!.IsCollectable)
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
                var sim = new Sim();
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
        // State *after* executing the action
        public ActionResponse Response { get; private set; }
        public SimulationState State { get; private set; }
        private Reliablity? Reliability { get; set; }

        public Step(ActionType action, Sim sim, in SimulationState lastState, out SimulationState newState)
        {
            Action = action;
            newState = Recalculate(sim, lastState);
        }

        public SimulationState Recalculate(Sim sim, in SimulationState lastState)
        {
            (Response, State) = sim.Execute(lastState, Action);
            Reliability = null;
            return State;
        }

        public Reliablity GetReliability(in SimulationState initialState, IEnumerable<ActionType> actionSet, RecipeData recipeData) =>
            Reliability ??=
                new(initialState, actionSet, Service.Configuration.ReliabilitySimulationCount, recipeData);
    };

    private List<Step> Macro { get; set; } = new();
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
    private List<Step> QueuedSteps { get; set; } = new();

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

    public Reliablity GetReliability(RecipeData recipeData) =>
        Macro.Count > 0 ?
            Macro[^1].GetReliability(InitialState, Macro.Select(m => m.Action), recipeData) :
            new(InitialState, Array.Empty<ActionType>(), 0, recipeData);

    public void RemoveRange(int index, int count) =>
        Macro.RemoveRange(index, count);

    public void Clear() =>
        Macro.Clear();

    public void Add(ActionType action)
    {
        var sim = CreateSim();
        Macro.Add(new(action, sim, State, out _));
    }

    public void Insert(int index, ActionType action)
    {
        if (index < 0 || index >= Macro.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var state = index == 0 ? InitialState : Macro[index - 1].State;
        var sim = CreateSim();
        Macro.Insert(index, new(action, sim, state, out state));

        for (var i = index + 1; i < Macro.Count; i++)
            state = Macro[i].Recalculate(sim, state);
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Macro.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Macro.RemoveAt(index);

        var state = index == 0 ? InitialState : Macro[index - 1].State;
        var sim = CreateSim();
        for (var i = index; i < Macro.Count; i++)
            state = Macro[i].Recalculate(sim, state);
    }

    public int Enqueue(ActionType action)
    {
        lock (QueueLock)
        {
            var lastState = QueuedSteps.Count > 0 ? QueuedSteps[^1].State : State;
            QueuedSteps.Add(new(action, CreateSim(), lastState, out _));
            return QueuedSteps.Count + Macro.Count;
        }
    }

    public void ClearQueue()
    {
        lock (QueueLock)
        {
            QueuedSteps.Clear();
        }
    }

    public void FlushQueue()
    {
        lock (QueueLock)
        {
            if (QueuedSteps.Count > 0)
            {
                Macro.AddRange(QueuedSteps);
                QueuedSteps.Clear();
            }
        }
    }

    public void RecalculateState()
    {
        var sim = CreateSim();
        var lastState = InitialState;
        for (var i = 0; i < Macro.Count; i++)
            lastState = Macro[i].Recalculate(sim, lastState);
    }

    private static Sim CreateSim() =>
        Service.Configuration.ConditionRandomness ? new Sim() : new SimNoRandom();
}
