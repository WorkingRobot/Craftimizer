using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver.Crafty;
using ObjectLayoutInspector;
using System.Diagnostics;

namespace Craftimizer.Benchmark;

internal static class Program
{
    private static void Main()
    {
        //TypeLayout.PrintLayout<ArenaNode<SimulationNode>>(true);
        //return;

        var input = new SimulationInput(
            new CharacterStats { Craftsmanship = 4041, Control = 3905, CP = 609, Level = 90 },
            new RecipeInfo()
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
        );

        var s = Stopwatch.StartNew();
        if (true)
            _ = Solver.Crafty.Solver.SearchStepwise(new(), input, a => Console.WriteLine(a));
        else
        {
            (var actions, _) = Solver.Crafty.Solver.SearchOneshot(new(), input);
            foreach (var action in actions)
                Console.Write($">{action.IntName()}");
            Console.WriteLine();
        }
        s.Stop();
        Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}");
    }
}
