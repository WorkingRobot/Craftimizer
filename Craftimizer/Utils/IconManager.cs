using Craftimizer.Plugin;
using Dalamud.Interface.Internal;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Craftimizer.Utils;

public sealed class IconManager : IDisposable
{
    private readonly Dictionary<uint, IDalamudTextureWrap> iconCache = [];
    private readonly Dictionary<uint, IDalamudTextureWrap> hqIconCache = [];
    private readonly Dictionary<string, IDalamudTextureWrap> textureCache = [];
    private readonly Dictionary<string, IDalamudTextureWrap> assemblyCache = [];

    public IDalamudTextureWrap GetIcon(uint id)
    {
        if (!iconCache.TryGetValue(id, out var ret))
            iconCache.Add(id, ret = Service.TextureProvider.GetIcon(id) ??
                throw new ArgumentException($"Invalid icon id {id}", nameof(id)));
        return ret;
    }

    public IDalamudTextureWrap GetHqIcon(uint id, bool isHq = true)
    {
        if (!isHq)
            return GetIcon(id);
        if (!hqIconCache.TryGetValue(id, out var ret))
            hqIconCache.Add(id, ret = Service.TextureProvider.GetIcon(id, ITextureProvider.IconFlags.HiRes | ITextureProvider.IconFlags.ItemHighQuality) ??
                throw new ArgumentException($"Invalid hq icon id {id}", nameof(id)));
        return ret;
    }

    public IDalamudTextureWrap GetTexture(string path)
    {
        if (!textureCache.TryGetValue(path, out var ret))
            textureCache.Add(path, ret = Service.TextureProvider.GetTextureFromGame(path) ??
            throw new ArgumentException($"Invalid texture {path}", nameof(path)));
        return ret;
    }

    public IDalamudTextureWrap GetAssemblyTexture(string filename)
    {
        if (!assemblyCache.TryGetValue(filename, out var ret))
            assemblyCache.Add(filename, ret = GetAssemblyTextureInternal(filename));
        return ret;
    }

    private static IDalamudTextureWrap GetAssemblyTextureInternal(string filename)
    {
        var assembly = Assembly.GetExecutingAssembly();
        byte[] iconData;
        using (var stream = assembly.GetManifestResourceStream($"Craftimizer.{filename}") ?? throw new InvalidDataException($"Could not load resource {filename}"))
        {
            iconData = new byte[stream.Length];
            _ = stream.Read(iconData);
        }
        return Service.PluginInterface.UiBuilder.LoadImage(iconData);
    }

    public void Dispose()
    {
        foreach (var image in iconCache.Values)
            image.Dispose();
        iconCache.Clear();

        foreach (var image in hqIconCache.Values)
            image.Dispose();
        hqIconCache.Clear();

        foreach (var image in textureCache.Values)
            image.Dispose();
        textureCache.Clear();

        foreach (var image in assemblyCache.Values)
            image.Dispose();
        assemblyCache.Clear();
    }
}
