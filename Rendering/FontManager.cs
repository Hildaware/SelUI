using System.IO;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace SelUI.Rendering;

/// <summary>
///     Owns SelUI's font atlas. Fonts are baked once at a constant high resolution and scaled down
///     at draw time, which keeps text crisp at any requested size. The bundled Miedinger font is the
///     default; any <see cref="SingleFontSpec" /> the user picks is baked on demand and cached.
///     Adapted from PrettyFly's FontManager.
/// </summary>
public sealed class FontManager : IDisposable
{
    private const float BakedSize = 48f;

    private readonly IFontAtlas _atlas;
    private readonly Dictionary<SingleFontSpec, IFontHandle> _customHandles = new();
    private readonly IFontHandle _defaultHandle;

    public FontManager(IDalamudPluginInterface pluginInterface)
    {
        _atlas = pluginInterface.UiBuilder.FontAtlas;
        var fontPath = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "Media", "Fonts", "Miedinger.ttf");

        _defaultHandle = _atlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit =>
        {
            e.Font = toolkit.AddFontFromFile(fontPath, new SafeFontConfig { SizePx = BakedSize });
        }));
    }

    /// <summary>The resolution every font is rasterized at. Divide your target size by this to get the draw scale.</summary>
    public static float AtlasBakedSize => BakedSize;

    public bool Ready => _defaultHandle.Available;

    public void Dispose()
    {
        _defaultHandle.Dispose();
        foreach (var h in _customHandles.Values) h.Dispose();
    }

    /// <summary>Get a font handle for the given spec, or the bundled default when <paramref name="spec" /> is null.</summary>
    public IFontHandle GetHandle(SingleFontSpec? spec)
    {
        if (spec == null) return _defaultHandle;
        if (_customHandles.TryGetValue(spec, out var h)) return h;
        h = (spec with { SizePx = BakedSize }).CreateFontHandle(_atlas);
        _customHandles[spec] = h;
        return h;
    }

    /// <summary>Release a cached custom font handle (e.g. when the user resets back to the default).</summary>
    public void ReleaseHandle(SingleFontSpec? spec)
    {
        if (spec != null && _customHandles.Remove(spec, out var h)) h.Dispose();
    }
}
