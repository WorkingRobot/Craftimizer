using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Solver;

internal static class Trace
{
    [Conditional("IS_TRACE")]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Log(string msg) =>
        Console.WriteLine(msg);
}
