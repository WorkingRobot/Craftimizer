using Dalamud.Interface.Colors;
using ImGuiNET;
using System.Numerics;

namespace Craftimizer.Utils;

public static class Colors
{
    public static readonly Vector4 Progress = new(0.44f, 0.65f, 0.18f, 1f);
    public static readonly Vector4 Quality = new(0.26f, 0.71f, 0.69f, 1f);
    public static readonly Vector4 Durability = new(0.13f, 0.52f, 0.93f, 1f);
    public static readonly Vector4 HQ = new(0.592f, 0.863f, 0.376f, 1f);
    public static readonly Vector4 CP = new(0.63f, 0.37f, 0.75f, 1f);

    private static Vector4 SolverProgressBg => ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TableBorderLight));
    private static Vector4 SolverProgressFgBland => ImGuiColors.DalamudWhite2;
    private static readonly Vector4[] SolverProgressFg = new Vector4[]
    {
        new(0.87f, 0.19f, 0.30f, 1f),
        new(0.96f, 0.62f, 0.12f, 1f),
        new(0.97f, 0.84f, 0.00f, 1f),
        new(0.37f, 0.69f, 0.35f, 1f),
        new(0.21f, 0.30f, 0.98f, 1f),
        new(0.26f, 0.62f, 0.94f, 1f),
        new(0.70f, 0.49f, 0.88f, 1f),
    };

    public static (Vector4 Background, Vector4 Foreground) GetSolverProgressColors(int? stageValue) => 
        stageValue is not { } stage ?
            (SolverProgressBg, SolverProgressFgBland) :
            stage == 0 ?
                (SolverProgressBg, SolverProgressFg[0]) :
                (SolverProgressFg[(stage - 1) % SolverProgressFg.Length], SolverProgressFg[stage % SolverProgressFg.Length]);
}
