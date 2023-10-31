using Craftimizer.Simulator.Actions;

namespace Craftimizer.Test.Solver;

[TestClass]
public class ActionSetTests
{
    [TestMethod]
    public void TestAcceptedActions()
    {
        var actions = Craftimizer.Solver.Simulator.AcceptedActions;
        var lut = Craftimizer.Solver.Simulator.AcceptedActionsLUT;

        Assert.IsTrue(actions.Length <= 32);
        foreach(var i in Enum.GetValues<ActionType>())
        {
            var idx = lut[(byte)i];
            if (idx != 0)
                Assert.AreEqual(i, actions[idx]);
        }
    }

    [TestMethod]
    public void TestSize()
    {
        var set = new ActionSet();
        Assert.IsTrue(set.IsEmpty);
        Assert.AreEqual(0, set.Count);

        set.AddAction(ActionType.BasicSynthesis);
        set.AddAction(ActionType.WasteNot2);
        
        Assert.AreEqual(2, set.Count);
        Assert.IsFalse(set.IsEmpty);

        set.RemoveAction(ActionType.BasicSynthesis);
        set.RemoveAction(ActionType.WasteNot2);

        Assert.IsTrue(set.IsEmpty);
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void TestAddRemove()
    {
        var set = new ActionSet();

        Assert.IsTrue(set.AddAction(ActionType.BasicSynthesis));
        Assert.IsFalse(set.AddAction(ActionType.BasicSynthesis));

        Assert.IsTrue(set.RemoveAction(ActionType.BasicSynthesis));
        Assert.IsFalse(set.RemoveAction(ActionType.BasicSynthesis));
        
        Assert.IsTrue(set.AddAction(ActionType.BasicSynthesis));
        Assert.IsTrue(set.AddAction(ActionType.WasteNot2));

        Assert.IsTrue(set.RemoveAction(ActionType.BasicSynthesis));
        Assert.IsTrue(set.RemoveAction(ActionType.WasteNot2));
    }

    [TestMethod]
    public void TestHasAction()
    {
        var set = new ActionSet();

        set.AddAction(ActionType.BasicSynthesis);

        Assert.IsTrue(set.HasAction(ActionType.BasicSynthesis));
        Assert.IsFalse(set.HasAction(ActionType.WasteNot2));

        set.AddAction(ActionType.WasteNot2);
        Assert.IsTrue(set.HasAction(ActionType.BasicSynthesis));
        Assert.IsTrue(set.HasAction(ActionType.WasteNot2));

        set.RemoveAction(ActionType.BasicSynthesis);
        Assert.IsFalse(set.HasAction(ActionType.BasicSynthesis));
        Assert.IsTrue(set.HasAction(ActionType.WasteNot2));
    }

    [TestMethod]
    public void TestElementAt()
    {
        var set = new ActionSet();

        set.AddAction(ActionType.BasicSynthesis);
        set.AddAction(ActionType.ByregotsBlessing);
        set.AddAction(ActionType.DelicateSynthesis);
        set.AddAction(ActionType.FocusedTouch);

        Assert.AreEqual(3, set.Count);

        Assert.AreEqual(ActionType.DelicateSynthesis, set.ElementAt(0));
        Assert.AreEqual(ActionType.FocusedTouch, set.ElementAt(1));
        Assert.AreEqual(ActionType.ByregotsBlessing, set.ElementAt(2));
        Assert.AreEqual(ActionType.BasicSynthesis, set.ElementAt(3));

        set.RemoveAction(ActionType.FocusedTouch);

        Assert.AreEqual(3, set.Count);
        Assert.AreEqual(ActionType.DelicateSynthesis, set.ElementAt(0));
        Assert.AreEqual(ActionType.ByregotsBlessing, set.ElementAt(1));
        Assert.AreEqual(ActionType.BasicSynthesis, set.ElementAt(2));
    }
}
