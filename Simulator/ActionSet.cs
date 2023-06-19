using Craftimizer.Simulator.Actions;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Craftimizer.Simulator;

public record ActionSet
{
    public ulong Bits { get; set; }
    public List<ActionType> SavedActions { get; set; } = new();

    private bool HasFlagA(ActionType action) => (Bits & (1ul << ((int)action + 1))) != 0;
    private bool HasFlagB(ActionType action) => SavedActions.Contains(action);
    public bool HasFlag(ActionType action)
    {
        var a = HasFlagA(action);
        var b = HasFlagB(action);
        if (a != b)
            throw new Exception($"Action {action} has different flags: {a} vs {b}");
        return a;
    }

    private void SetFlagA(ActionType action) => Bits |= 1ul << ((int)action + 1);
    private void SetFlagB(ActionType action)
    {
        if (!SavedActions.Contains(action))
            SavedActions.Add(action);
    }
    public void SetFlag(ActionType action)
    {
        SetFlagA(action);
        SetFlagB(action);
    }

    private void ClearFlagA(ActionType action) => Bits &= ~(1ul << ((int)action + 1));
    private void ClearFlagB(ActionType action) => SavedActions.RemoveAll(a => a == action);
    public void ClearFlag(ActionType action)
    {
        ClearFlagA(action);
        ClearFlagB(action);
    }

    public IEnumerable<ActionType> Actions => GetActions();

    private IEnumerable<ActionType> GetActions()
    {
        foreach (var action in Enum.GetValues<ActionType>())
            if (HasFlag(action))
                yield return action;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NthBitSet(ulong value, int n)
    {
        if (Bmi2.X64.IsSupported)
            return BitOperations.TrailingZeroCount(Bmi2.X64.ParallelBitDeposit(1ul << n, value));

        ulong mask = 0x0000FFFFFFFFu;
        var size = 32;
        var _base = 0;

        if (n++ >= BitOperations.PopCount(value))
            return 64;

        while (size > 0)
        {
            var count = BitOperations.PopCount(value & mask);
            if (n > count)
            {
                _base += size;
                size >>= 1;
                mask |= mask << size;
            }
            else
            {
                size >>= 1;
                mask >>= size;
            }
        }

        return _base;
    }

    private ActionType ActionAtA(int index) => Actions.ElementAt(index);//(ActionType)(NthBitSet(Bits, index) - 1);
    private ActionType ActionAtB(int index) => SavedActions.ElementAt(index);
    public ActionType ActionAt(int index)
    {
        return ActionAtB(index);
        var a = ActionAtA(index);
        var a2 = (ActionType)(NthBitSet(Bits, index) - 1);
        var b = ActionAtB(index);
        if (a != a2)
            throw new Exception($"A2: Action {index} has different flags: {a} vs {a2}");
        if (a != b)
            throw new Exception($"Action {index} has different flags: {a} vs {b}");
        return a;
    }

    private int ActionCountA => BitOperations.PopCount(Bits);
    private int ActionCountB => SavedActions.Count;
    public int ActionCount { get
        {
            return ActionCountB;
            var a = ActionCountA;
            var b = ActionCountB;
            if (a != b)
                throw new Exception($"Action count has different flags: {a} vs {b}");
            return a;
        } }

    private bool IsEmptyA => Bits == 0;
    private bool IsEmptyB => SavedActions.Count == 0;
    public bool IsEmpty
    {
        get
        {
            return IsEmptyB;
            var a = IsEmptyA;
            var b = IsEmptyB;
            if (a != b)
                throw new Exception($"IsEmpty has different flags: {a} vs {b}");
            return a;
        }
    }
}
