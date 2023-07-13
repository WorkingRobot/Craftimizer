namespace Craftimizer.Simulator.Actions;

// Observe -> Focused Synthesis
internal sealed class FocusedSynthesisCombo : BaseAction
{
    public override ActionCategory Category => ActionCategory.Combo;
    public override int Level => 67;
    public override uint ActionId => 100235;

    public override bool IncreasesProgress => true;

    public override int CPCost(Simulator s) => 7 + 5;

    public override bool CanUse(Simulator s) =>
        //              Observe.DurabilityCost v
        base.CanUse(s) && VerifyDurability2(s, 0);

    private static readonly Observe ActionA = new();
    private static readonly FocusedSynthesis ActionB = new();
    public override void Use(Simulator s)
    {
        s.ExecuteForced(ActionType.Observe, ActionA);
        ActionB.Use(s);
    }

    public override string GetTooltip(Simulator s, bool addUsability) =>
        $"{ActionA.GetTooltip(s, addUsability)}\n{ActionB.GetTooltip(s, addUsability)}";
}
