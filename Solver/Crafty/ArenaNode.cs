using System.Runtime.CompilerServices;

namespace Craftimizer.Solver.Crafty;

public class ArenaNode<T> where T : struct
{
    public T State;
    public readonly List<ArenaNode<T>> Children;
    public readonly ArenaNode<T>? Parent;

    public ArenaNode(T state, ArenaNode<T>? parent = null)
    {
        State = state;
        Children = new();
        Parent = parent;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArenaNode<T> Add(T state)
    {
        var node = new ArenaNode<T>(state, this);
        Children.Add(node);
        return node;
    }
}
