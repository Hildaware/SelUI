using System.IO;
using System.Linq;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace SelUI.Rendering;

/// <summary>
///     Owns SelUI's font atlas. Fonts are baked once at a constant high resolution and scaled down
///     at draw time, which keeps text crisp at any requested size. SelUI bundles a handful of fonts in
///     Media/Fonts (Grotesk is the default); any of those can be the active bundled font, and any
///     <see cref="SingleFontSpec" /> the user picks from the system chooser is baked on demand and cached.
///     Adapted from PrettyFly's FontManager.
/// </summary>
public sealed class FontManager : IDisposable
{
    private const float BakedSize = 48f;

    /// <summary>The bundled font used when no other is selected.</summary>
    public const string DefaultBundledFont = "Grotesk";

    private readonly IFontAtlas _atlas;
    private readonly Dictionary<string, IFontHandle> _bundledHandles = new();
    private readonly Dictionary<SingleFontSpec, IFontHandle> _customHandles = new();
    private readonly string _fontDir;

    private string _activeBundledFont = DefaultBundledFont;

    public FontManager(IDalamudPluginInterface pluginInterface)
    {
        _atlas = pluginInterface.UiBuilder.FontAtlas;
        _fontDir = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "Media", "Fonts");

        // Enumerate the bundled .ttf files so dropping a new font in Media/Fonts makes it selectable
        // with no code change. The default sorts first; the rest follow alphabetically.
        BundledFontNames = Directory.Exists(_fontDir)
            ? Directory.GetFiles(_fontDir, "*.ttf")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n == DefaultBundledFont ? 0 : 1)
                .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : new[] { DefaultBundledFont };
    }

    /// <summary>The resolution every font is rasterized at. Divide your target size by this to get the draw scale.</summary>
    public static float AtlasBakedSize => BakedSize;

    /// <summary>Names (without extension) of the fonts bundled in Media/Fonts, default first.</summary>
    public IReadOnlyList<string> BundledFontNames { get; }

    /// <summary>
    ///     The bundled font used when no system font is picked. Set to a name from
    ///     <see cref="BundledFontNames" />; falls back to <see cref="DefaultBundledFont" /> when null/unknown.
    /// </summary>
    public string ActiveBundledFont
    {
        get => _activeBundledFont;
        set => _activeBundledFont = !string.IsNullOrEmpty(value) && BundledFontNames.Contains(value)
            ? value
            : DefaultBundledFont;
    }

    public bool Ready => GetBundledHandle(_activeBundledFont).Available;

    public void Dispose()
    {
        foreach (var h in _bundledHandles.Values) h.Dispose();
        foreach (var h in _customHandles.Values) h.Dispose();
    }

    /// <summary>Get a font handle for the given spec, or the active bundled font when <paramref name="spec" /> is null.</summary>
    public IFontHandle GetHandle(SingleFontSpec? spec)
    {
        if (spec == null) return GetBundledHandle(_activeBundledFont);
        if (_customHandles.TryGetValue(spec, out var h)) return h;
        h = (spec with { SizePx = BakedSize }).CreateFontHandle(_atlas);
        _customHandles[spec] = h;
        return h;
    }

    /// <summary>Release a cached custom font handle (e.g. when the user resets back to a bundled font).</summary>
    public void ReleaseHandle(SingleFontSpec? spec)
    {
        if (spec != null && _customHandles.Remove(spec, out var h)) h.Dispose();
    }

    /// <summary>Bake (once) and return the handle for the named bundled font.</summary>
    private IFontHandle GetBundledHandle(string name)
    {
        if (_bundledHandles.TryGetValue(name, out var h)) return h;
        var path = Path.Combine(_fontDir, name + ".ttf");
        h = _atlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit =>
        {
            e.Font = toolkit.AddFontFromFile(path, new SafeFontConfig { SizePx = BakedSize });
        }));
        _bundledHandles[name] = h;
        return h;
    }
}
