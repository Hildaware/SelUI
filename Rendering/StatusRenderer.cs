using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using SelUI.Modules.UnitFrames;

namespace SelUI.Rendering;

/// <summary>
///     Draws a unit's status effects as a grid of icons (with duration and stack count), filtered to
///     either buffs or debuffs. Icons are cropped to the clean inner art, sorted by remaining time, and
///     buffs you applied can be cancelled with a right-click. Each collection lives in its own overlay
///     window so it can be positioned independently of the bars.
/// </summary>
public sealed class StatusRenderer
{
    private const float Gap = 4f;

    private readonly Dictionary<uint, ISharedImmediateTexture> _iconCache = new();
    private readonly LabelRenderer _labels;
    private readonly IObjectTable _objects;
    private readonly ITextureProvider _textures;

    public StatusRenderer(LabelRenderer labels, ITextureProvider textures, IObjectTable objects)
    {
        _labels = labels;
        _textures = textures;
        _objects = objects;
    }

    public void Draw(string id, StatusListConfig cfg, Vector2 frameOrigin, IBattleChara battle, bool buffs, float alpha)
    {
        if (!cfg.Enabled) return;

        var me = _objects.LocalPlayer;
        var myObjectId = me?.GameObjectId ?? 0;
        var myEntityId = me?.EntityId ?? 0;

        var items = new List<Item>();
        foreach (var status in battle.StatusList)
        {
            if (status == null || status.StatusId == 0) continue;
            if (status.GameData.ValueNullable is not { } data || data.Icon == 0) continue;
            if (data.StatusCategory == 1 != buffs) continue;
            if (cfg.CleansableOnly && !data.CanDispel) continue;

            var mine = myObjectId != 0 && status.SourceObject?.GameObjectId == myObjectId;
            if (cfg.OwnOnly && !mine) continue;

            var stacks = data.MaxStacks > 0 && status.Param > 0 ? status.Param : 0;
            // Cropped icons use the base art (stacks shown as text); uncropped use the per-stack icon.
            var iconId = cfg.CropIcon ? data.Icon : (uint)(data.Icon + Math.Max(0, stacks - 1));
            items.Add(new Item(iconId, status.RemainingTime, stacks, status.StatusId, mine));
        }

        if (items.Count == 0) return;

        // Sort by remaining time (soonest first); permanent / no-timer statuses go last.
        items.Sort((a, b) => SortKey(a.Time).CompareTo(SortKey(b.Time)));
        if (items.Count > cfg.MaxIcons) items.RemoveRange(cfg.MaxIcons, items.Count - cfg.MaxIcons);

        RenderItems(id, cfg, frameOrigin, items, buffs, myEntityId, alpha);
    }

    /// <summary>Mock-data path for config previews: draws the given icon ids with placeholder timers.</summary>
    public void DrawPreview(string id, StatusListConfig cfg, Vector2 frameOrigin, IReadOnlyList<uint> iconIds, float alpha)
    {
        if (!cfg.Enabled || iconIds.Count == 0) return;

        var items = new List<Item>();
        for (var i = 0; i < iconIds.Count && i < cfg.MaxIcons; i++)
            items.Add(new Item(iconIds[i], MathF.Max(1f, 30f - i * 4f), 0, 0, false));

        RenderItems(id, cfg, frameOrigin, items, false, 0, alpha);
    }

    private void RenderItems(string id, StatusListConfig cfg, Vector2 frameOrigin, List<Item> items, bool interactive, uint myEntityId, float alpha)
    {
        var size = cfg.IconSize;
        var step = size + Gap;
        var perLine = Math.Max(1, cfg.PerLine);

        var positions = new Vector2[items.Count];
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (var i = 0; i < items.Count; i++)
        {
            var col = i % perLine;
            var row = i / perLine;
            var dx = cfg.GrowRight ? col * step : -col * step;
            var dy = cfg.GrowDown ? row * step : -row * step;
            var p = frameOrigin + cfg.Position + new Vector2(dx, dy);
            positions[i] = p;
            minX = MathF.Min(minX, p.X);
            minY = MathF.Min(minY, p.Y);
            maxX = MathF.Max(maxX, p.X + size);
            maxY = MathF.Max(maxY, p.Y + size);
        }

        // Extra right/bottom room so the timer (larger, anchored on each icon's bottom-right corner)
        // isn't clipped by the window.
        var overflow = _labels.Scale(cfg.FontSize) + 6f;
        var winPos = new Vector2(minX - 4f, minY - 4f);
        var winSize = new Vector2(maxX - minX + 4f + overflow, maxY - minY + 4f + overflow);

        DrawHelper.DrawInWindow(id, winPos, winSize, interactive, dl =>
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var pos = positions[i];
                var wrap = GetIcon(item.Icon).GetWrapOrEmpty();

                var (uv0, uv1) = cfg.CropIcon
                    ? (new Vector2(4f / wrap.Width, 14f / wrap.Height),
                        new Vector2(1f - 4f / wrap.Width, 1f - 12f / wrap.Height))
                    : (Vector2.Zero, Vector2.One);
                dl.AddImage(wrap.Handle, pos, pos + new Vector2(size), uv0, uv1, Colors.MultiplyAlpha(0xFFFFFFFFu, alpha));

                // Timer: right-justified, anchored to the icon's bottom-right corner, and a touch
                // larger than the rest of the status text.
                if (cfg.ShowDuration && item.Time > 0f)
                    _labels.Draw(dl, FormatDuration(item.Time), pos + new Vector2(size + 4f, size),
                        cfg.FontSize + 4f, Colors.White, DrawAnchor.Right, alpha: alpha);

                if (cfg.ShowStacks && item.Stacks > 1)
                    _labels.Draw(dl, item.Stacks.ToString(), new Vector2(pos.X + size, pos.Y),
                        cfg.FontSize, Colors.White, DrawAnchor.TopRight, alpha: alpha);

                // Right-click to cancel your own buffs.
                if (interactive && item.Mine && myEntityId != 0 &&
                    ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(size)) &&
                    ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    StatusManager.ExecuteStatusOff(item.StatusId, myEntityId);
            }
        });
    }

    private static float SortKey(float time)
    {
        return time <= 0f ? float.MaxValue : time;
    }

    private static string FormatDuration(float seconds)
    {
        if (seconds >= 3600f) return $"{(int)(seconds / 3600f)}h";
        if (seconds >= 60f) return $"{(int)(seconds / 60f)}m";
        return ((int)seconds).ToString();
    }

    private ISharedImmediateTexture GetIcon(uint iconId)
    {
        if (_iconCache.TryGetValue(iconId, out var tex)) return tex;
        tex = _textures.GetFromGameIcon(new GameIconLookup(iconId));
        _iconCache[iconId] = tex;
        return tex;
    }

    private readonly record struct Item(uint Icon, float Time, int Stacks, uint StatusId, bool Mine);
}
