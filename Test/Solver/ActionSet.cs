using System.Runtime.CompilerServices;

namespace Craftimizer.Test.Solver;

[TestClass]
public class ActionSetTests
{
    private readonly ActionPool pool = ActionPool.Default;

    [TestMethod]
    public void TestActionPoolSize()
    {
        Assert.AreEqual(ActionPool.EnumSize, Enum.GetValues<ActionType>().Length);
        Assert.AreEqual(ActionPool.MaskSize, Unsafe.SizeOf<ActionSet>() * 8);
    }

    [TestMethod]
    public void TestAcceptedActions()
    {
        foreach (var i in Enum.GetValues<ActionType>())
        {
            byte idx;
            try
            {
                idx = pool.FromAction(i);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }
            Assert.AreEqual(i, pool.ToAction(idx));
        }
    }

    [TestMethod]
    public void TestSize()
    {
        var set = new ActionSet();
        Assert.IsTrue(set.IsEmpty);
        Assert.AreEqual(0, set.Count);

        set.AddAction(in pool, ActionType.BasicSynthesis);
        set.AddAction(in pool, ActionType.WasteNot2);

        Assert.AreEqual(2, set.Count);
        Assert.IsFalse(set.IsEmpty);

        set.RemoveAction(in pool, ActionType.BasicSynthesis);
        set.RemoveAction(in pool, ActionType.WasteNot2);

        Assert.IsTrue(set.IsEmpty);
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void TestAddRemove()
    {
        var set = new ActionSet();

        Assert.IsTrue(set.AddAction(in pool, ActionType.BasicSynthesis));
        Assert.IsFalse(set.AddAction(in pool, ActionType.BasicSynthesis));

        Assert.IsTrue(set.RemoveAction(in pool, ActionType.BasicSynthesis));
        Assert.IsFalse(set.RemoveAction(in pool, ActionType.BasicSynthesis));

        Assert.IsTrue(set.AddAction(in pool, ActionType.BasicSynthesis));
        Assert.IsTrue(set.AddAction(in pool, ActionType.WasteNot2));

        Assert.IsTrue(set.RemoveAction(in pool, ActionType.BasicSynthesis));
        Assert.IsTrue(set.RemoveAction(in pool, ActionType.WasteNot2));
    }

    [TestMethod]
    public void TestHasAction()
    {
        var set = new ActionSet();

        set.AddAction(in pool, ActionType.BasicSynthesis);

        Assert.IsTrue(set.HasAction(in pool, ActionType.BasicSynthesis));
        Assert.IsFalse(set.HasAction(in pool, ActionType.WasteNot2));

        set.AddAction(in pool, ActionType.WasteNot2);
        Assert.IsTrue(set.HasAction(in pool, ActionType.BasicSynthesis));
        Assert.IsTrue(set.HasAction(in pool, ActionType.WasteNot2));

        set.RemoveAction(in pool, ActionType.BasicSynthesis);
        Assert.IsFalse(set.HasAction(in pool, ActionType.BasicSynthesis));
        Assert.IsTrue(set.HasAction(in pool, ActionType.WasteNot2));
    }

    [TestMethod]
    public void TestElementAt()
    {
        var set = new ActionSet();

        set.AddAction(in pool, ActionType.BasicSynthesis);
        set.AddAction(in pool, ActionType.ByregotsBlessing);
        set.AddAction(in pool, ActionType.DelicateSynthesis);
        set.AddAction(in pool, ActionType.Reflect);

        Assert.AreEqual(4, set.Count);

        Assert.AreEqual(ActionType.DelicateSynthesis, set.ElementAt(in pool, 0));
        Assert.AreEqual(ActionType.Reflect, set.ElementAt(in pool, 1));
        Assert.AreEqual(ActionType.ByregotsBlessing, set.ElementAt(in pool, 2));
        Assert.AreEqual(ActionType.BasicSynthesis, set.ElementAt(in pool, 3));

        set.RemoveAction(in pool, ActionType.Reflect);

        Assert.AreEqual(3, set.Count);

        Assert.AreEqual(ActionType.DelicateSynthesis, set.ElementAt(in pool, 0));
        Assert.AreEqual(ActionType.ByregotsBlessing, set.ElementAt(in pool, 1));
        Assert.AreEqual(ActionType.BasicSynthesis, set.ElementAt(in pool, 2));
    }

    [TestMethod]
    public void TestRandomIndex()
    {
#if IS_DETERMINISTIC
        Assert.Inconclusive("Craftimizer is currently built for determinism; all random actions are not actually random.");
#endif

        var actions = new[]
        {
            ActionType.BasicTouch,
            ActionType.BasicSynthesis,
            ActionType.GreatStrides,
            ActionType.TrainedFinesse,
        };

        var set = new ActionSet();
        foreach(var action in actions)
            set.AddAction(in pool, action);

        var counts = new Dictionary<ActionType, int>();
        var rng = new Random(0);
        for (var i = 0; i < 100; i++)
        {
            var action = set.SelectRandom(in pool, rng);

            CollectionAssert.Contains(actions, action);

            counts[action] = counts.GetValueOrDefault(action) + 1;
        }

        foreach (var action in actions)
            Assert.IsTrue(counts.GetValueOrDefault(action) > 0);
    }
}
