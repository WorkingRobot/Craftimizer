namespace Craftimizer.Test.Simulator;

[TestClass]
public class SimulatorTests
{
    // https://craftingway.app/rotation/loud-namazu-jVe9Y
    // Chondrite Saw
    private static SimulationInput Input1 { get; } =
        new(new()
        {
            Craftsmanship = 3304,
            Control = 3374,
            CP = 575,
            Level = 90,
            CanUseManipulation = true,
            HasSplendorousBuff = false,
            IsSpecialist = false,
            CLvl = 560,
        },
        new()
        {
            IsExpert = false,
            ClassJobLevel = 90,
            RLvl = 560,
            ConditionsFlag = 0b1111,
            MaxDurability = 80,
            MaxQuality = 7200,
            MaxProgress = 3500,
            QualityModifier = 80,
            QualityDivider = 115,
            ProgressModifier = 90,
            ProgressDivider = 130
        });

    // https://craftingway.app/rotation/sandy-fafnir-doVCs
    // Classical Longsword
    private static SimulationInput Input2 { get; } =
        new(new()
        {
            Craftsmanship = 3290,
            Control = 3541,
            CP = 649,
            Level = 90,
            CanUseManipulation = true,
            HasSplendorousBuff = false,
            IsSpecialist = false,
            CLvl = 560,
        },
        new()
        {
            IsExpert = false,
            ClassJobLevel = 90,
            RLvl = 580,
            ConditionsFlag = 0b1111,
            MaxDurability = 70,
            MaxQuality = 10920,
            MaxProgress = 3900,
            QualityModifier = 70,
            QualityDivider = 115,
            ProgressModifier = 80,
            ProgressDivider = 130
        });

    private static SimulationState AssertCraft(SimulationInput input, IEnumerable<ActionType> actions,
        int progress, int quality,
        int durability, int cp)
    {
        var simulator = new SimulatorNoRandom();
        var (_, state, _) = simulator.ExecuteMultiple(new(input), actions);
        Assert.AreEqual(progress, state.Progress);
        Assert.AreEqual(quality, state.Quality);
        Assert.AreEqual(durability, state.Durability);
        Assert.AreEqual(cp, state.CP);
        return state;
    }

    [TestMethod]
    public void BasicActions()
    {
        AssertCraft(
            Input1,
            new[] {
                ActionType.BasicTouch,
                ActionType.BasicSynthesis,
                ActionType.MastersMend
            },
            276, 262, 80, 469);
    }

    [TestMethod]
    public void BasicTouchCombo()
    {
        AssertCraft(
            Input1,
            new[] {
                ActionType.Innovation,
                ActionType.BasicTouch,
                ActionType.StandardTouch,
                ActionType.AdvancedTouch,
                ActionType.StandardTouch,
                ActionType.AdvancedTouch
            },
            0, 2828, 30, 425);
    }

    [TestMethod]
    public void WithBuffs1()
    {
        AssertCraft(
            Input1,
            new[] {
                ActionType.Reflect,
                ActionType.Manipulation,
                ActionType.PreparatoryTouch,
                ActionType.WasteNot2
            },
            0, 1414, 60, 335);
    }

    [TestMethod]
    public void WithBuffs2()
    {
        AssertCraft(
            Input1,
            new[] {
                ActionType.MuscleMemory,
                ActionType.GreatStrides,
                ActionType.PrudentTouch,
                ActionType.DelicateSynthesis
            },
            1150, 812, 55, 480);
    }

    [TestMethod]
    public void WithBuffs3()
    {
        AssertCraft(
            Input1,
            new[] {
                ActionType.MuscleMemory,
                ActionType.Manipulation,
                ActionType.MastersMend,
                ActionType.WasteNot2,
                ActionType.Innovation,
                ActionType.DelicateSynthesis,
                ActionType.BasicTouch,
                ActionType.GreatStrides,
                ActionType.ByregotsBlessing
            },
            1150, 1925, 80, 163);
    }

    [TestMethod]
    public void TrainedFinesseProcs()
    {
        var state = AssertCraft(
            Input1,
            new[] {
                ActionType.Reflect,
                ActionType.WasteNot,
                ActionType.PreparatoryTouch,
                ActionType.PreparatoryTouch,
                ActionType.BasicTouch,
                ActionType.StandardTouch,
                ActionType.PrudentTouch,
                ActionType.PreparatoryTouch
            },
            0, 4588, 15, 332);
        Assert.AreEqual(10, state.ActiveEffects.InnerQuiet);
        Assert.IsTrue(ActionType.TrainedFinesse.Base().CanUse(new SimulatorNoRandom() { State = state }));
    }

    [TestMethod]
    public void TestCompletedCraft1()
    {
        AssertCraft(
            Input1,
            new[] {
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
            },
            3726, 8748, 5, 69);
    }

    [TestMethod]
    public void TestCompletedCraft2()
    {
        AssertCraft(
            Input2,
            new[] {
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
            },
            // TC           https://ffxivteamcraft.com/simulator/35020/34800/4PTlwTV6w1aGCUdO2BRl
            // Craftingway  https://craftingway.app/rotation/sandy-fafnir-doVCs
            3549, 10932, 5, 7);
    }
}
