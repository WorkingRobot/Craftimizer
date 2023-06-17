namespace Craftimizer.Simulator;

public enum Condition : ushort
{
    Poor = 0x0008,
    Normal = 0x0001,
    Good = 0x0002,
    Excellent = 0x0004,

    Centered = 0x0010,
    Sturdy = 0x0020,
    Pliant = 0x0040,
    Malleable = 0x0080,
    Primed = 0x0100,
    GoodOmen = 0x0200,
}

public static class ConditionUtils
{
    public static Condition[] GetPossibleConditions(ushort conditionsFlag) =>
        Enum.GetValues<Condition>().Where(c => ((Condition)conditionsFlag).HasFlag(c)).ToArray();
}
