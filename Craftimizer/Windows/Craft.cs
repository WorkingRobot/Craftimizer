using Craftimizer.Plugin.Utils;
using Craftimizer.Simulator;
using Craftimizer.Utils;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using System.Numerics;

namespace Craftimizer.Plugin.Windows;

public unsafe class Craft : Window
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration
      | ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoSavedSettings
      | ImGuiWindowFlags.NoFocusOnAppearing
      | ImGuiWindowFlags.NoNavFocus;

    private RecipeNote RecipeUtils { get; } = new();

    private bool WasOpen { get; set; }

    private CharacterStats CharacterStats { get; set; } = null!;

    public Craft() : base("Craftimizer SynthesisHelper", WindowFlags, true)
    {
        Service.WindowSystem.AddWindow(this);

        IsOpen = true;
    }

    public override void Draw()
    {
        ImGui.Text($"{CharacterStats.CP};{CharacterStats.Control};{CharacterStats.Craftsmanship}");
    }

    public override void PreDraw()
    {
        var addon = RecipeUtils.AddonSynthesis;
        ref var unit = ref addon->AtkUnitBase;
        var scale = unit.Scale;
        var pos = new Vector2(unit.X, unit.Y);
        var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

        var node = unit.GetNodeById(5);

        Position = pos + new Vector2(size.X, node->Y * scale);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(-1),
            MaximumSize = new(10000, 10000)
        };

        base.PreDraw();
    }

    private bool DrawConditionsInner()
    {
        if (!RecipeUtils.Update(out _))
            return false;
        return false;
        if (RecipeUtils.AddonSynthesis == null)
            return false;

        // Check if Synthesis addon is visible
        if (RecipeUtils.AddonSynthesis->AtkUnitBase.WindowNode == null)
            return false;

        return base.DrawConditions();
    }

    public override bool DrawConditions()
    {
        var ret = DrawConditionsInner();
        if (ret && !WasOpen)
            ResetSimulation();

        WasOpen = ret;
        return ret;
    }

    private void ResetSimulation()
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            return;

        CharacterStats = Gearsets.CalculateCharacterStats(Gearsets.CalculateGearsetCurrentStats(), Gearsets.GetGearsetItems(container), RecipeUtils.CharacterLevel, RecipeUtils.CanUseManipulation);
    }
}
