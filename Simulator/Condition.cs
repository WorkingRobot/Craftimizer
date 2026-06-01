namespace Craftimizer.Simulator;

public enum Condition : byte
{
    Normal,
    Good,
    Excellent,
    Poor,

    Centered,
    Sturdy,
    Pliant,
    Malleable,
    Primed,
    GoodOmen,
    Robust,
}

public static class ConditionUtils
{
    [Flags]
    private enum ConditionMask : ushort
    {
        Normal    = 1 << 0, // 0x0001
        Good      = 1 << 1, // 0x0002
        Excellent = 1 << 2, // 0x0004
        Poor      = 1 << 3, // 0x0008

        Centered  = 1 << 4, // 0x0010
        Sturdy    = 1 << 5, // 0x0020
        Pliant    = 1 << 6, // 0x0040
        Malleable = 1 << 7, // 0x0080
        Primed    = 1 << 8, // 0x0100
        GoodOmen  = 1 << 9, // 0x0200
        Robust    = 1 << 10, // 0x0400
    }

    public static Condition[] GetPossibleConditions(ushort conditionsFlag) =>
        Enum.GetValues<Condition>().Where(c => ((ConditionMask)conditionsFlag).HasFlag((ConditionMask)(1 << (ushort)c))).ToArray();
}
