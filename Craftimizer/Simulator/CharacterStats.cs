using System.Linq;
using Craftimizer.Plugin;

namespace Craftimizer.Simulator;

internal record CharacterStats
{
    public int Craftsmanship { get; }
    public int Control { get; }
    public int CP { get; }
    public int Level { get; }

    public int CLvl => Level <= 80
            ? LuminaSheets.ParamGrowSheet.GetRow((uint)Level)!.CraftingLevel
            : (int)LuminaSheets.RecipeLevelTableSheet.First(r => r.ClassJobLevel == Level).RowId;
}
