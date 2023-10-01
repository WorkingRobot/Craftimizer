using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Craftimizer.Utils;

public static class SqText
{
    private static ReadOnlyDictionary<char, SeIconChar> levelNumReplacements = new(new Dictionary<char, SeIconChar>
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
        foreach(var (k, v) in levelNumReplacements)
            str = str.Replace(k, v.ToIconChar());
        return SeIconChar.LevelEn.ToIconChar() + str;
    }
}
