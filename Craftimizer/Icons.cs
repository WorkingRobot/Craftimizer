using ImGuiScene;
using System.Collections.Generic;

namespace Craftimizer.Plugin;

internal static class Icons
{
    private static readonly Dictionary<string, TextureWrap> Cache = new();

    public static TextureWrap GetIconFromPath(string path)
    {
        if (!Cache.TryGetValue(path, out var ret))
            Cache.Add(path, ret = Service.DataManager.GetImGuiTexture(path)!);
        return ret;
    }

    public static TextureWrap GetIconFromId(ushort id) =>
        GetIconFromPath($"ui/icon/{id / 1000 * 1000:000000}/{id:000000}_hr1.tex");
}
