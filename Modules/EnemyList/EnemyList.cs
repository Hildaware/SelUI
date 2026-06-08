using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using SelUI.Game;
using SelUI.Modules.UnitFrames;
using SelUI.Rendering;

namespace SelUI.Modules.EnemyList;

/// <summary>
///     The enemy list: a stack of compact frames for aggro'd enemies, each reusing the shared unit
///     frame plus a threat (enmity) icon. Clickable / mouseover like the other frames.
/// </summary>
public sealed class EnemyList : IHudModule, IMovableModule
{
    private const float ThreatIconSize = 20f;
    private static readonly string[] GrowthItems = ["Down", "Up"]; // index 0 = down, 1 = up (matches GrowUp)

    private readonly EnemyListConfig _config;
    private readonly UnitFrame _frame;
    private readonly EnemyListHelper _helper;
    private readonly IObjectTable _objects;
    private readonly RenderScale _scale;
    private readonly ITargetManager _targets;
    private readonly ITextureProvider _textures;
    private readonly IReadOnlyList<uint> _mockDebuffIcons;
    private readonly Action<IGameObject> _onHover;
    private readonly Action<IGameObject> _onLeftClick;
    private readonly Action<IGameObject> _onRightClick;
    private ISharedImmediateTexture? _threatTexture;

    public EnemyList(EnemyListConfig config, EnemyListHelper helper, IObjectTable objects, ITargetManager targets,
        MouseoverManager mouseover, UnitFrame frame, ITextureProvider textures, IReadOnlyList<uint> mockDebuffIcons, RenderScale scale)
    {
        _config = config;
        _helper = helper;
        _objects = objects;
        _targets = targets;
        _frame = frame;
        _textures = textures;
        _scale = scale;
        _mockDebuffIcons = mockDebuffIcons;
        _onLeftClick = actor => _targets.Target = actor;
        _onRightClick = UnitInteraction.OpenContextMenu;
        _onHover = mouseover.SetHovered;
    }

    public string Name => "Enemy List";

    public ModuleConfig Config => _config;

    public string EditLabel => Name;

    public Vector2 EditTopLeft => _config.GrowUp
        ? _config.Position - new Vector2(0f, (_config.MaxRows - 1) * RowPitch)
        : _config.Position;

    public Vector2 EditSize => new(_config.Row.Width * _scale.Value, RowPitch * _config.MaxRows);
    public void MoveBy(Vector2 delta) => _config.Position += delta;

    /// <summary>Vertical pitch between rows, scaled with the UI so spacing tracks the (scaled) row size.</summary>
    private float RowPitch => _config.RowHeight * _scale.Value;

    /// <summary>Top-left of row <paramref name="index" />, stacking up or down per config.</summary>
    private Vector2 RowOrigin(int index) =>
        _config.Position + new Vector2(0f, (_config.GrowUp ? -1f : 1f) * index * RowPitch);

    public void Dispose()
    {
    }

    private const int PreviewRows = 8;

    public void Draw()
    {
        if (_config.PreviewMode)
        {
            DrawPreview();
            return;
        }

        _helper.Update();
        var enemies = _helper.Enemies;
        var rows = Math.Min(enemies.Count, _config.MaxRows);
        if (rows == 0) return;

        var actors = new IGameObject?[rows];
        var origins = new Vector2[rows];
        for (var i = 0; i < rows; i++)
        {
            actors[i] = _objects.SearchByEntityId(enemies[i].EntityId);
            origins[i] = RowOrigin(i);
        }

        // Two passes so every row's debuffs sit above every row's bar (no cross-row clipping).
        for (var i = 0; i < rows; i++)
            _frame.Draw($"SelUI_Enemy{i}", _config.Row, actors[i], origins[i], IsSelected(actors[i]),
                _onLeftClick, _onRightClick, _onHover, drawStatuses: false);

        for (var i = 0; i < rows; i++)
            _frame.DrawStatuses($"SelUI_Enemy{i}", _config.Row, actors[i], origins[i]);

        for (var i = 0; i < rows; i++)
            DrawThreatIcon(origins[i], enemies[i].Enmity);
    }

    private void DrawPreview()
    {
        var units = new PreviewUnit[PreviewRows];
        var origins = new Vector2[PreviewRows];
        for (var i = 0; i < PreviewRows; i++)
        {
            units[i] = new PreviewUnit
            {
                Name = $"Enemy {i + 1}",
                Color = UnitColors.Hostile,
                HpFraction = 1f - i * 0.1f,
                DebuffIcons = _mockDebuffIcons
            };
            origins[i] = RowOrigin(i);
        }

        for (var i = 0; i < PreviewRows; i++)
            _frame.Draw($"SelUI_Enemy{i}", _config.Row, null, origins[i], false, preview: units[i], drawStatuses: false);

        for (var i = 0; i < PreviewRows; i++)
            _frame.DrawStatuses($"SelUI_Enemy{i}", _config.Row, null, origins[i], units[i]);

        for (var i = 0; i < PreviewRows; i++)
            DrawThreatIcon(origins[i], i % 4 + 1);
    }

    public bool DrawConfig()
    {
        var changed = false;

        var pos = _config.Position;
        if (ImGui.DragFloat2("Position", ref pos))
        {
            _config.Position = pos;
            changed = true;
        }

        var spacing = _config.RowHeight;
        if (ImGui.DragFloat("Frame spacing", ref spacing, 0.5f, 10f, 200f, "%.0f"))
        {
            _config.RowHeight = spacing;
            changed = true;
        }

        var grow = _config.GrowUp ? 1 : 0;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.Combo("Growth direction", ref grow, GrowthItems, GrowthItems.Length))
        {
            _config.GrowUp = grow == 1;
            changed = true;
        }

        var preview = _config.PreviewMode;
        if (ImGui.Checkbox("Show preview", ref preview))
        {
            _config.PreviewMode = preview;
            changed = true;
        }

        if (ImGui.CollapsingHeader("Row style"))
        {
            using var indent = ImRaii.PushIndent();
            changed |= UI.UnitFrameConfigUI.Draw(_config.Row);
        }

        return changed;
    }

    private bool IsSelected(IGameObject? actor)
    {
        if (actor == null) return false;
        return (_targets.Target != null && _targets.Target.Address == actor.Address)
               || (_targets.SoftTarget != null && _targets.SoftTarget.Address == actor.Address);
    }

    private void DrawThreatIcon(Vector2 origin, int enmity)
    {
        if (enmity < 1) return;

        _threatTexture ??= _textures.GetFromGame("ui/uld/enemylist_hr1.tex");
        var wrap = _threatTexture.GetWrapOrEmpty();
        if (wrap.Handle == IntPtr.Zero || wrap.Width == 0) return;

        // The enmity icons are a 48px sprite row at v = 0.48 in the enemy-list texture (cells 0..3).
        var cell = Math.Min(3, enmity - 1);
        var w = 48f / wrap.Width;
        var h = 48f / wrap.Height;
        var uv0 = new Vector2(w * cell, 0.48f);
        var uv1 = new Vector2(w * (cell + 1), 0.48f + h);

        // Centered on the bar's left edge, vertically centered on the bar (header 14 + bar 20 => 24), +2 down.
        // Scaled with the UI so it tracks the (scaled) row.
        var size = new Vector2(ThreatIconSize * _scale.Value);
        var pos = origin + new Vector2(0f, 26f * _scale.Value) - size / 2f;

        // Foreground draw list so it always sits above the health bar.
        ImGui.GetForegroundDrawList().AddImage(wrap.Handle, pos, pos + size, uv0, uv1);
    }
}
