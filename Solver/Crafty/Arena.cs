using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public class Arena<T> where T : struct
{
    public readonly struct Node
    {
        public readonly T State;
        public readonly List<int> Children;
        public readonly int Parent;

        public Node(T state, int parent)
        {
            State = state;
            Children = new();
            Parent = parent;
        }
    }

    private readonly List<Node> nodes = new();

    public Arena(T initialState = default)
    {
        nodes.Add(new(initialState, -1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Insert(int parentIndex, T state)
    {
        var index = nodes.Count;
        nodes.Add(new(state, parentIndex));
        nodes[parentIndex].Children.Add(index);
        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Node Get(int index) => nodes[index];
}
