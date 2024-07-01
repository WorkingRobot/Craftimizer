using Craftimizer.Plugin;
using Dalamud.Interface.Textures;
using System;
using System.Reflection;

namespace Craftimizer.Utils;

public static class IconManager
{
    public static ISharedImmediateTexture GetIcon(uint id, bool isHq = false)
    {
        return Service.TextureProvider.GetFromGameIcon(new GameIconLookup(id, itemHq: isHq));
    }

    public static ISharedImmediateTexture GetTexture(string path)
    {
        return Service.TextureProvider.GetFromGame(path);
    }

    public static ISharedImmediateTexture GetAssemblyTexture(string filename)
    {
        return Service.TextureProvider.GetFromManifestResource(Assembly.GetExecutingAssembly(), $"Craftimizer.{filename}");
    }

    public static nint GetHandle(this ISharedImmediateTexture me) =>
        me.GetWrapOrEmpty().ImGuiHandle;
}
