using System.Runtime.InteropServices;

namespace Craftimizer.Simulator;

[StructLayout(LayoutKind.Auto)]
public record struct SimulationState
{
    public readonly SimulationInput Input;

    public int ActionCount;
    public int StepCount;
    public int Progress;
    public int Quality;
    public int Durability;
    public int CP;
    public Condition Condition;
    public Effects ActiveEffects;
    public ActionStates ActionStates;

    // https://github.com/ffxiv-teamcraft/simulator/blob/0682dfa76043ff4ccb38832c184d046ceaff0733/src/model/tables.ts#L2
    private static ReadOnlySpan<int> HQPercentTable => new[] {
        1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8,
        9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 16, 16, 17, 17,
        17, 18, 18, 18, 19, 19, 20, 20, 21, 22, 23, 24, 26, 28, 31, 34, 38, 42, 47, 52, 58, 64, 68, 71,
        74, 76, 78, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 94, 96, 98, 100
    };
    public readonly int HQPercent => HQPercentTable[(int)Math.Clamp((float)Quality / Input.Recipe.MaxQuality * 100, 0, 100)];
    public readonly int Collectability => Math.Max(Quality / 10, 1);
    public readonly int MaxCollectability => Math.Max(Input.Recipe.MaxQuality / 10, 1);

    public readonly bool IsFirstStep => StepCount == 0;

    public SimulationState(SimulationInput input)
    {
        Input = input;

        StepCount = 0;
        Progress = 0;
        Quality = input.StartingQuality;
        Durability = Input.Recipe.MaxDurability;
        CP = Input.Stats.CP;
        Condition = Condition.Normal;
        ActiveEffects = new();
        ActionCount = 0;
        ActionStates = new();
    }
}
