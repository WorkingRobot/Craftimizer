using System.Linq;
using Craftimizer.Plugin;

namespace Craftimizer.Simulator;

public record CharacterStats
{
    public int Craftsmanship { get; init; }
    public int Control { get; init; }
    public int CP { get; init; }
    public int Level { get; init; }

    public int CLvl => Level <= 80
            ? LuminaSheets.ParamGrowSheet.GetRow((uint)Level)!.CraftingLevel
            : (int)LuminaSheets.RecipeLevelTableSheet.First(r => r.ClassJobLevel == Level).RowId;
}
