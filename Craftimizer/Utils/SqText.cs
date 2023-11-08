using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Craftimizer.Utils;

public static class SqText
{
    public static SeIconChar LevelPrefix => SeIconChar.LevelEn;

    public static readonly ReadOnlyDictionary<char, SeIconChar> LevelNumReplacements = new(new Dictionary<char, SeIconChar>
    {
        ['0'] = SeIconChar.Number0,
        ['1'] = SeIconChar.Number1,
        ['2'] = SeIconChar.Number2,
        ['3'] = SeIconChar.Number3,
        ['4'] = SeIconChar.Number4,
        ['5'] = SeIconChar.Number5,
        ['6'] = SeIconChar.Number6,
        ['7'] = SeIconChar.Number7,
        ['8'] = SeIconChar.Number8,
        ['9'] = SeIconChar.Number9,
    });

    public static string ToLevelString<T>(T value) where T : IBinaryInteger<T>
    {
        var str = value.ToString() ?? throw new FormatException("Failed to format value");
        foreach(var (k, v) in LevelNumReplacements)
            str = str.Replace(k, v.ToIconChar());
        return str;
    }

    public static bool TryParseLevelString(string str, out int result)
    {
        foreach(var (k, v) in LevelNumReplacements)
            str = str.Replace(v.ToIconChar(), k);
        return int.TryParse(str, out result);
    }
}
