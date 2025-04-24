namespace Craftimizer.Test.Simulator;

[TestClass]
public class RotationSimulatorTests
{
    // https://craftingway.app/rotation/loud-namazu-jVe9Y
    // Chondrite Saw
    private static SimulationInput Input1 { get; } = CreateInput(3304, 3374, 575, 90, 80, 7200, 3500, 80, 115, 90, 130);

    // https://craftingway.app/rotation/sandy-fafnir-doVCs
    // Classical Longsword
    private static SimulationInput Input2 { get; } = CreateInput(3290, 3541, 649, 90, 70, 10920, 3900, 70, 115, 80, 130);

    private static SimulationInput CreateInput(
        int craftsmanship, int control, int cp,
        int level, int durability, int quality, int progress,
        int qualityModifier, int qualityDivider, int progressModifier, int progressDivider)
    {
        return new(new()
        {
            Craftsmanship = craftsmanship,
            Control = control,
            CP = cp,
            Level = level,
            CanUseManipulation = true,
            HasSplendorousBuff = false,
            IsSpecialist = false,
        },
        new()
        {
            IsExpert = false,
            ClassJobLevel = level,
            MaxDurability = durability,
            MaxQuality = quality,
            MaxProgress = progress,
            QualityModifier = qualityModifier,
            QualityDivider = qualityDivider,
            ProgressModifier = progressModifier,
            ProgressDivider = progressDivider
        });
    }

    private static SimulationState AssertCraft(SimulationInput input, ActionType[] actions,
        int progress, int quality,
        int durability, int cp)
    {
        var simulator = new RotationSimulatorNoRandom();
        var (_, state, _) = simulator.ExecuteMultiple(new(input), actions);
        Assert.AreEqual(progress, state.Progress);
        Assert.AreEqual(quality, state.Quality);
        Assert.AreEqual(durability, state.Durability);
        Assert.AreEqual(cp, state.CP);
        return state;
    }

    [TestMethod]
    public void BasicActions() =>
        AssertCraft(
            Input1,
            [
                ActionType.BasicTouch,
                ActionType.BasicSynthesis,
                ActionType.MastersMend
            ],
            276, 262, 80, 469);

    [TestMethod]
    public void BasicTouchCombo() =>
        AssertCraft(
            Input1,
            [
                ActionType.Innovation,
                ActionType.BasicTouch,
                ActionType.StandardTouch,
                ActionType.AdvancedTouch,
                ActionType.StandardTouch,
                ActionType.AdvancedTouch
            ],
            0, 2828, 30, 425);

    [TestMethod]
    public void WithBuffs1() =>
        AssertCraft(
            Input1,
            [
                ActionType.Reflect,
                ActionType.Manipulation,
                ActionType.PreparatoryTouch,
                ActionType.WasteNot2
            ],
            0, 1414, 60, 335);

    [TestMethod]
    public void WithBuffs2() =>
        AssertCraft(
            Input1,
            [
                ActionType.MuscleMemory,
                ActionType.GreatStrides,
                ActionType.PrudentTouch,
                ActionType.DelicateSynthesis
            ],
            1150, 812, 55, 480);

    [TestMethod]
    public void WithBuffs3() =>
        AssertCraft(
            Input1,
            [
                ActionType.MuscleMemory,
                ActionType.Manipulation,
                ActionType.MastersMend,
                ActionType.WasteNot2,
                ActionType.Innovation,
                ActionType.DelicateSynthesis,
                ActionType.BasicTouch,
                ActionType.GreatStrides,
                ActionType.ByregotsBlessing
            ],
            1150, 1925, 80, 163);

    [TestMethod]
    public void TrainedFinesseProcs()
    {
        var state = AssertCraft(
            Input1,
            [
                ActionType.Reflect,
                ActionType.WasteNot,
                ActionType.PreparatoryTouch,
                ActionType.PreparatoryTouch,
                ActionType.BasicTouch,
                ActionType.StandardTouch,
                ActionType.PrudentTouch,
                ActionType.PreparatoryTouch
            ],
            0, 4588, 15, 332);
        Assert.AreEqual(10, state.ActiveEffects.InnerQuiet);
        Assert.IsTrue(ActionType.TrainedFinesse.Base().CanUse(new RotationSimulatorNoRandom() { State = state }));
    }

    [TestMethod]
    public void TestCompletedCraft1() =>
        AssertCraft(
            Input1,
            [
                ActionType.Reflect,
                ActionType.Manipulation,
                ActionType.PreparatoryTouch,
                ActionType.WasteNot2,
                ActionType.PreparatoryTouch,
                ActionType.Innovation,
                ActionType.PreparatoryTouch,
                ActionType.PreparatoryTouch,
                ActionType.GreatStrides,
                ActionType.ByregotsBlessing,
                ActionType.Veneration,
                ActionType.Groundwork,
                ActionType.Groundwork,
                ActionType.Groundwork,
            ],
            3726, 8748, 5, 69);

    [TestMethod]
    public void TestCompletedCraft2() =>
        AssertCraft(
            Input2,
            [
                ActionType.MuscleMemory,
                ActionType.Manipulation,
                ActionType.Veneration,
                ActionType.WasteNot2,
                ActionType.Groundwork,
                ActionType.Groundwork,
                ActionType.StandardTouch,
                ActionType.Innovation,
                ActionType.PreparatoryTouch,
                ActionType.PreparatoryTouch,
                ActionType.PreparatoryTouch,
                ActionType.PreparatoryTouch,
                ActionType.GreatStrides,
                ActionType.Innovation,
                ActionType.PreparatoryTouch,
                ActionType.TrainedFinesse,
                ActionType.GreatStrides,
                ActionType.ByregotsBlessing
            ],
            // TC           https://ffxivteamcraft.com/simulator/35020/34800/4PTlwTV6w1aGCUdO2BRl
            // Craftingway  https://craftingway.app/rotation/sandy-fafnir-doVCs
            3549, 10932, 5, 7);

    // Should handle Reflect properly
    [TestMethod]
    public void TestTeamcraft1()
    {
        var state = AssertCraft(
            CreateInput(2278, 2348, 532, 80, 80, 866, 31, 100, 30, 100, 50),
            [
                ActionType.Reflect,
                ActionType.BasicTouch,
                ActionType.CarefulSynthesis,
            ],
            685, 3431, 50, 501);

        Assert.AreEqual(3, state.ActiveEffects.InnerQuiet);
    }

    // Should provide same result as ingame for a low level rotation
    [TestMethod]
    public void TestTeamcraft2() =>
        AssertCraft(
            CreateInput(2278, 2348, 532, 80, 80, 866, 31, 100, 30, 100, 50),
            [
                ActionType.Reflect,
                ActionType.BasicTouch,
                ActionType.ByregotsBlessing,
                ActionType.CarefulSynthesis,
            ],
            685, 5130, 40, 477);

    // Should handle new Innovation interactions properly
    [TestMethod]
    public void TestTeamcraft3() =>
        AssertCraft(
            CreateInput(2763, 2780, 545, 80, 80, 5200, 2000, 100, 105, 100, 121),
            [
                ActionType.Reflect,
                ActionType.DelicateSynthesis,
                ActionType.DelicateSynthesis,
                ActionType.WasteNot,
                ActionType.Groundwork,
                ActionType.Innovation,
                ActionType.PreparatoryTouch,
                ActionType.PreparatoryTouch,
                ActionType.MastersMend,
                ActionType.PreparatoryTouch,
            ],
            1150, 5947, 30, 175);

    // Should compute flooring accurately
    [TestMethod]
    public void TestTeamcraft4A() =>
        AssertCraft(
            CreateInput(1645, 1532, 400, 80, 80, 5200, 2000, 100, 105, 100, 121),
            [
                ActionType.BasicTouch,
                ActionType.BasicTouch,
                ActionType.BasicTouch,
                ActionType.BasicTouch,
            ],
            0, 828, 40, 328);

    [TestMethod]
    public void TestTeamcraft4B() =>
        AssertCraft(
            CreateInput(3289, 3420, 400, 90, 80, 10920, 3900, 70, 115, 80, 130),
            [
                ActionType.MuscleMemory,
                ActionType.Veneration,
                ActionType.Groundwork,
                ActionType.Groundwork,
                ActionType.Observe,
                ActionType.Observe,
                ActionType.CarefulSynthesis,
            ],
            3897, 0, 20, 319);

    // Should compute flooring accurately using DT rotation
    [TestMethod]
    public void TestTeamcraft5A() =>
        AssertCraft(
            CreateInput(3957, 3896, 563, 94, 80, 11400, 6300, 100, 147, 100, 167),
            [
                ActionType.Reflect,
                ActionType.Innovation,
                ActionType.PreparatoryTouch,
                ActionType.PrudentTouch,
            ],
            0, 2610, 45, 474);

    [TestMethod]
    public void TestTeamcraft5B() =>
        AssertCraft(
            CreateInput(4045, 3902, 601, 100, 80, 11400, 6300, 100, 147, 100, 167),
            [
                ActionType.Reflect,
                ActionType.Innovation,
                ActionType.PreparatoryTouch,
                ActionType.PrudentTouch,
                ActionType.GreatStrides,
                ActionType.PreparatoryTouch,
                ActionType.GreatStrides,
                ActionType.Innovation,
                ActionType.PreparatoryTouch,
                ActionType.ImmaculateMend,
                ActionType.GreatStrides,
                ActionType.ByregotsBlessing,
                ActionType.WasteNot,
                ActionType.Veneration,
                ActionType.Groundwork,
                ActionType.Groundwork,
                ActionType.Groundwork,
                ActionType.Groundwork,
                ActionType.Veneration,
                ActionType.Groundwork,
            ],
            6585, 11400, 0, 0);
}
