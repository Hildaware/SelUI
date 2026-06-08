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
    private const float Gap = 4f; // gap between status icons, at the reference UI scale

    private readonly Dictionary<uint, ISharedImmediateTexture> _iconCache = new();
    private readonly LabelRenderer _labels;
    private readonly IObjectTable _objects;
    private readonly RenderScale _scale;
    private readonly ITextureProvider _textures;

    public StatusRenderer(LabelRenderer labels, ITextureProvider textures, IObjectTable objects, RenderScale scale)
    {
        _labels = labels;
        _textures = textures;
        _objects = objects;
        _scale = scale;
    }

    public void Draw(string id, StatusListConfig cfg, Vector2 frameOrigin, IBattleChara battle, bool buffs, float alpha)
    {
        if (!cfg.Enabled) return;

        var items = Collect(cfg, battle, buffs);
        if (items.Count == 0) return;

        if (items.Count > cfg.MaxIcons) items.RemoveRange(cfg.MaxIcons, items.Count - cfg.MaxIcons);
        RenderItems(id, cfg, frameOrigin, items, true, _objects.LocalPlayer?.EntityId ?? 0, alpha);
    }

    /// <summary>
    ///     Draw debuffs and buffs as one continuous grid (debuffs first), laid out with <paramref name="layout" />.
    ///     Each group keeps its own filters (<paramref name="debuffs" /> / <paramref name="buffs" />) and is sorted
    ///     by remaining time; the window is interactive so your own buffs stay right-click-cancellable.
    /// </summary>
    public void DrawCombined(string id, StatusListConfig layout, StatusListConfig debuffs, StatusListConfig buffs,
        Vector2 frameOrigin, IBattleChara battle, float alpha)
    {
        if (!layout.Enabled) return;

        var items = Collect(debuffs, battle, false);
        items.AddRange(Collect(buffs, battle, true));
        if (items.Count == 0) return;

        if (items.Count > layout.MaxIcons) items.RemoveRange(layout.MaxIcons, items.Count - layout.MaxIcons);
        RenderItems(id, layout, frameOrigin, items, true, _objects.LocalPlayer?.EntityId ?? 0, alpha);
    }

    /// <summary>Filtered, sorted status items for one category. Caller applies the icon cap and renders.</summary>
    private List<Item> Collect(StatusListConfig cfg, IBattleChara battle, bool buffs)
    {
        if (!cfg.Enabled) return new List<Item>();

        var myObjectId = _objects.LocalPlayer?.GameObjectId ?? 0;

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
            items.Add(new Item(iconId, status.RemainingTime, stacks, status.StatusId, mine, buffs,
                data.Name.ExtractText(), data.Description.ExtractText()));
        }

        // Sort by remaining time (soonest first); permanent / no-timer statuses go last.
        items.Sort((a, b) => SortKey(a.Time).CompareTo(SortKey(b.Time)));
        return items;
    }

    /// <summary>Mock-data path for config previews: draws the given icon ids with placeholder timers.</summary>
    public void DrawPreview(string id, StatusListConfig cfg, Vector2 frameOrigin, IReadOnlyList<uint> iconIds, float alpha)
    {
        if (!cfg.Enabled || iconIds.Count == 0) return;

        var items = new List<Item>();
        for (var i = 0; i < iconIds.Count && i < cfg.MaxIcons; i++)
            items.Add(new Item(iconIds[i], MathF.Max(1f, 30f - i * 4f), 0, 0, false, false, string.Empty, string.Empty));

        RenderItems(id, cfg, frameOrigin, items, false, 0, alpha);
    }

    /// <summary>Mock-data path for the combined buffs+debuffs grid (debuffs first), for config previews.</summary>
    public void DrawCombinedPreview(string id, StatusListConfig layout, Vector2 frameOrigin,
        IReadOnlyList<uint> debuffIcons, IReadOnlyList<uint> buffIcons, float alpha)
    {
        if (!layout.Enabled) return;

        var items = new List<Item>();
        foreach (var icon in debuffIcons) items.Add(new Item(icon, 0f, 0, 0, false, false, string.Empty, string.Empty));
        foreach (var icon in buffIcons) items.Add(new Item(icon, 0f, 0, 0, false, true, string.Empty, string.Empty));
        if (items.Count == 0) return;

        if (items.Count > layout.MaxIcons) items.RemoveRange(layout.MaxIcons, items.Count - layout.MaxIcons);
        // Re-time so the preview shows a descending countdown across the whole grid.
        for (var i = 0; i < items.Count; i++) items[i] = items[i] with { Time = MathF.Max(1f, 30f - i * 4f) };

        RenderItems(id, layout, frameOrigin, items, false, 0, alpha);
    }

    private void RenderItems(string id, StatusListConfig cfg, Vector2 frameOrigin, List<Item> items, bool input, uint myEntityId, float alpha)
    {
        var v = _scale.Value;
        var size = cfg.IconSize * v;
        var step = size + Gap * v;
        var perLine = Math.Max(1, cfg.PerLine);

        var positions = new Vector2[items.Count];
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (var i = 0; i < items.Count; i++)
        {
            var col = i % perLine;
            var row = i / perLine;
            var dx = cfg.GrowRight ? col * step : -col * step;
            var dy = cfg.GrowDown ? row * step : -row * step;
            var p = frameOrigin + cfg.Position * v + new Vector2(dx, dy);
            positions[i] = p;
            minX = MathF.Min(minX, p.X);
            minY = MathF.Min(minY, p.Y);
            maxX = MathF.Max(maxX, p.X + size);
            maxY = MathF.Max(maxY, p.Y + size);
        }

        // Extra right/bottom room so the timer (larger, anchored on each icon's bottom-right corner)
        // isn't clipped by the window.
        var overflow = _labels.Scale(cfg.FontSize) + 6f * v;
        var pad = 4f * v;
        var winPos = new Vector2(minX - pad, minY - pad);
        var winSize = new Vector2(maxX - minX + pad + overflow, maxY - minY + pad + overflow);

        DrawHelper.DrawInWindow(id, winPos, winSize, input, dl =>
        {
            var windowHovered = input && ImGui.IsWindowHovered();

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
                    _labels.Draw(dl, FormatDuration(item.Time), pos + new Vector2(size + 4f * v, size),
                        cfg.FontSize + 4f, Colors.White, DrawAnchor.Right, alpha: alpha);

                if (cfg.ShowStacks && item.Stacks > 1)
                    _labels.Draw(dl, item.Stacks.ToString(), new Vector2(pos.X + size, pos.Y),
                        cfg.FontSize, Colors.White, DrawAnchor.TopRight, alpha: alpha);

                if (!windowHovered || !ImGui.IsMouseHoveringRect(pos, pos + new Vector2(size))) continue;

                // Tooltip (name + description) at the cursor for any status with game data.
                if (item.Name.Length > 0)
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(item.Name);
                    if (item.Description.Length > 0)
                    {
                        ImGui.Separator();
                        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                        ImGui.TextUnformatted(item.Description);
                        ImGui.PopTextWrapPos();
                    }

                    ImGui.EndTooltip();
                }

                // Right-click to cancel your own buffs.
                if (item.Buff && item.Mine && myEntityId != 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
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

    private readonly record struct Item(uint Icon, float Time, int Stacks, uint StatusId, bool Mine, bool Buff, string Name, string Description);
}
