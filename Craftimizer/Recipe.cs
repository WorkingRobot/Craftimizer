using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Craftimizer;

internal record Recipe
{
    public int Difficulty { get; init; }
    public int Durability { get; init; }
    public int Quality { get; init; }
    public int Progress { get; init; }
    public int Level { get; init; }
    public int MaxLevel { get; init; }
    public int ID { get; init; }
}
