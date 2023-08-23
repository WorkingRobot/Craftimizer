using Dalamud.Interface.Internal;
using ImGuiScene;
using System.Collections.Generic;

namespace Craftimizer.Plugin;

internal static class Icons
{
    private static readonly Dictionary<ushort, IDalamudTextureWrap> Cache = new();

    public static TextureWrap GetIconFromId(ushort id)
    {
        if (!Cache.TryGetValue(id, out var ret))
            Cache.Add(id, ret = Service.TextureProvider.GetIcon(id)!);
        return ret;
    }
}
