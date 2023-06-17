namespace Craftimizer.Solver.Crafty;

public class Arena<T> where T : struct
{
    public readonly record struct Node
    {
        public int? Parent { get; init; }
        public int Index { get; init; }
        public List<int> Children { get; init; }
        public T State { get; init; }
    }

    public List<Node> Nodes { get; } = new();

    public Arena(T initialState = default)
    {
        Nodes.Add(new() { Parent = null, Index = 0, Children = new(), State = initialState });
    }

    public int Insert(int parentIndex, T state)
    {
        var index = Nodes.Count;
        Nodes.Add(new() { Parent = parentIndex, Index = index, Children = new(), State = state });
        Nodes[parentIndex].Children.Add(index);
        return index;
    }

    public Node Get(int index) => Nodes[index];
}
