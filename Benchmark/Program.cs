using BenchmarkDotNet.Running;
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
        //var summary = BenchmarkRunner.Run<Bench>();
        //return;

        //TypeLayout.PrintLayout<ArenaNode<SimulationNode>>(true);
        //return;

        var input = new SimulationInput(
            new CharacterStats {
                Craftsmanship = 4041,
                Control = 3905,
                CP = 609,
                Level = 90,
                CanUseManipulation = true,
                HasSplendorousBuff = true,
                IsSpecialist = true,
                CLvl = 560,
            },
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
            },
            0
        );

        var config = new SolverConfig()
        {
            Iterations = 30_000,//1_000_000
            ThreadCount = 2,
        };

        var s = Stopwatch.StartNew();
        if (true)
            _ = Solver.Crafty.Solver.SearchStepwise(config, input, a => Console.WriteLine(a));
        else
        {
            (var actions, _) = Solver.Crafty.Solver.SearchOneshot(config, input);
            foreach (var action in actions)
                Console.Write($">{action.IntName()}");
            Console.WriteLine();
        }
        s.Stop();
        Console.WriteLine($"{s.Elapsed.TotalMilliseconds:0.00}");
    }
}
