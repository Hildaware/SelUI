using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace SelUI.Rendering;

/// <summary>
///     A small per-owner cache of game icons by id. Each renderer that draws icons keeps one, so the
///     <see cref="ITextureProvider.GetFromGameIcon" /> lookup for a given icon happens once rather than
///     every frame. The returned <see cref="ISharedImmediateTexture" /> is itself the long-lived handle;
///     callers still resolve it to a wrap at draw time.
/// </summary>
public sealed class IconCache
{
    private readonly Dictionary<uint, ISharedImmediateTexture> _cache = new();
    private readonly ITextureProvider _textures;

    public IconCache(ITextureProvider textures)
    {
        _textures = textures;
    }

    /// <summary>The shared texture for game icon <paramref name="iconId" />, cached after the first lookup.</summary>
    public ISharedImmediateTexture Get(uint iconId)
    {
        if (_cache.TryGetValue(iconId, out var tex)) return tex;
        tex = _textures.GetFromGameIcon(new GameIconLookup(iconId));
        _cache[iconId] = tex;
        return tex;
    }
}
