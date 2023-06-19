using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver.Crafty;
using ObjectLayoutInspector;

namespace Craftimizer.Benchmark;

internal static class Program
{
    private static void Main()
    {
        //TypeLayout.PrintLayout<Solver.Crafty.SimulationNode>(true);
        //return;

        var input = new SimulationInput()
        {
            Stats = new CharacterStats { Craftsmanship = 4041, Control = 3905, CP = 609, Level = 90 },
            Recipe = new RecipeInfo()
            {
                IsExpert = false,
                ClassJobLevel = 90,
                RLvl = 640,
                ConditionsFlag = 15,
                MaxDurability = 70,
                MaxQuality = 14040,
                MaxProgress = 6600,
                QualityModifier = 70,
                QualityDivider = 115,
                ProgressModifier = 80,
                ProgressDivider = 130,
            }
        };

        var actions = new List<ActionType>();
        if (true)
            (actions, _) = Solver.Crafty.Solver.SearchStepwise(input, actions, a => Console.WriteLine(a));
        else
        {
            (actions, _) = Solver.Crafty.Solver.SearchOneshot(input, actions);
            foreach (var action in actions)
                Console.Write($">{action.IntName()}");
            Console.WriteLine();
        }
    }
}
