using Craftimizer.Plugin;
using Dalamud.Bindings.ImGui;
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

    ImTextureID Handle { get; }
}

public interface ILoadedTextureIcon : ITextureIcon, IDisposable { }

public sealed class IconManager : IDisposable
{
    private sealed class LoadedIcon : ILoadedTextureIcon
    {
        public ISharedImmediateTexture Source { get; }

        public Vector2? Dimensions => GetWrap()?.Size;

        public ImTextureID Handle => GetWrapOrEmpty().Handle;

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

        public IDalamudTextureWrap GetWrapOrEmpty() => GetWrap() ?? Service.DalamudAssetManager.Empty4X4;

        public void Dispose()
        {
            DisposeToken.Cancel();
            TextureWrapTask.ToContentDisposedTask(true).Wait();
        }
    }

    // TODO: Unload when unused, but with a custom timer?
    private sealed class CachedIcon(ISharedImmediateTexture source) : ITextureIcon
    {
        private LoadedIcon Base { get; } = new(source);

        public ISharedImmediateTexture Source => Base.Source;

        public Vector2? Dimensions => Base.Dimensions;

        public ImTextureID Handle => Base.Handle;

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
