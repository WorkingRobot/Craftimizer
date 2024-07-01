using Craftimizer.Plugin;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Craftimizer.Utils;

public interface ITextureIcon
{
    ISharedImmediateTexture Source { get; }

    Vector2? Dimensions { get; }

    float? AspectRatio => Dimensions is { } d ? d.X / d.Y : null;

    nint ImGuiHandle { get; }
}

public interface ILoadedTextureIcon : ITextureIcon, IDisposable { }

public sealed class IconManager : IDisposable
{
    private sealed class LoadedIcon : ILoadedTextureIcon
    {
        // 10: DXGI_FORMAT_R16G16B16A16_FLOAT
        public static IDalamudTextureWrap EmptyTexture { get; } = Service.TextureProvider.CreateEmpty(new(4, 4, 10), false, false);

        public ISharedImmediateTexture Source { get; }

        public Vector2? Dimensions => GetWrap()?.Size;

        public nint ImGuiHandle => GetWrapOrEmpty().ImGuiHandle;

        private Task<IDalamudTextureWrap> TextureWrapTask { get; }
        private CancellationTokenSource DisposeToken { get; }

        public LoadedIcon(ISharedImmediateTexture source)
        {
            Source = source;
            DisposeToken = new();
            TextureWrapTask = source.RentAsync(DisposeToken.Token);
        }

        public IDalamudTextureWrap? GetWrap()
        {
            if (TextureWrapTask.IsCompletedSuccessfully)
                return TextureWrapTask.Result;
            return null;
        }

        public IDalamudTextureWrap GetWrapOrEmpty() => GetWrap() ?? EmptyTexture;

        public void Dispose()
        {
            DisposeToken.Cancel();
            TextureWrapTask.ToContentDisposedTask(true).Wait();
        }
    }

    // TODO: Unload when unused, but with a custom timer?
    private sealed class CachedIcon : ITextureIcon
    {
        private LoadedIcon Base { get; }

        public ISharedImmediateTexture Source => Base.Source;

        public Vector2? Dimensions => Base.Dimensions;

        public nint ImGuiHandle => Base.ImGuiHandle;

        public CachedIcon(ISharedImmediateTexture source)
        {
            Base = new(source);
        }

        public void Release()
        {
            Base.Dispose();
        }
    }

    private Dictionary<(uint Id, bool IsHq), CachedIcon> IconCache { get; } = [];
    private Dictionary<string, CachedIcon> AssemblyTextureCache { get; } = [];

    private static ISharedImmediateTexture GetIconInternal(uint id, bool isHq = false) =>
        Service.TextureProvider.GetFromGameIcon(new GameIconLookup(id, itemHq: isHq));

    private static ISharedImmediateTexture GetAssemblyTextureInternal(string filename) =>
        Service.TextureProvider.GetFromManifestResource(Assembly.GetExecutingAssembly(), $"Craftimizer.{filename}");

    public static ILoadedTextureIcon GetIcon(uint id, bool isHq = false) =>
        new LoadedIcon(GetIconInternal(id, isHq));

    public static ILoadedTextureIcon GetAssemblyTexture(string filename) =>
        new LoadedIcon(GetAssemblyTextureInternal(filename));

    public ITextureIcon GetIconCached(uint id, bool isHq = false)
    {
        if (IconCache.TryGetValue((id, isHq), out var icon))
            return icon;
        return IconCache[(id, isHq)] = new(GetIconInternal(id, isHq));
    }

    public ITextureIcon GetAssemblyTextureCached(string filename)
    {
        if (AssemblyTextureCache.TryGetValue(filename, out var texture))
            return texture;
        return AssemblyTextureCache[filename] = new(GetAssemblyTextureInternal(filename));
    }

    public void Dispose()
    {
        foreach (var value in IconCache.Values)
            value.Release();
        foreach (var value in AssemblyTextureCache.Values)
            value.Release();
    }
}
