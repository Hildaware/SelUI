using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility.Raii;
using SelUI.Modules.UnitFrames;
using SelUI.Rendering;

namespace SelUI.Modules.Statuses;

/// <summary>
///     The player's own buffs and debuffs, drawn independently of the player unit frame (the way the
///     cast bar is its own module). A single config entry ("Player Statuses") owns three
///     independently-positioned sub-grids — permanent (long) buffs, regular (short) buffs, and debuffs —
///     each with its own draggable edit box and on/off toggle. The grid look and filters are baked
///     (<see cref="StatusLayouts" />); only each grid's position, growth direction and enable flag are user state.
/// </summary>
public sealed class PlayerStatuses : IHudModule, IMovableModuleHost
{
    private readonly Func<IBattleChara?> _actorProvider;
    private readonly StatusListConfig _buffLayout;
    private readonly PlayerStatusesConfig _config;
    private readonly StatusListConfig _debuffLayout;
    private readonly IReadOnlyList<IMovableModule> _editParts;
    private readonly StatusListConfig _permanentLayout;
    private readonly StatusRenderer _statuses;

    private static readonly string[] GrowthItems = ["Right/Down", "Left/Down", "Right/Up", "Left/Up"];

    public PlayerStatuses(PlayerStatusesConfig config, Func<IBattleChara?> actorProvider, StatusRenderer statuses,
        StatusListConfig permanentLayout, StatusListConfig buffLayout, StatusListConfig debuffLayout, RenderScale scale)
    {
        _config = config;
        _actorProvider = actorProvider;
        _statuses = statuses;
        _permanentLayout = permanentLayout;
        _buffLayout = buffLayout;
        _debuffLayout = debuffLayout;
        _editParts = new IMovableModule[]
        {
            new StatusEditBox("Statuses (Long Duration)", config.Permanent, permanentLayout, scale),
            new StatusEditBox("Player Enhancements", config.Buffs, buffLayout, scale),
            new StatusEditBox("Player Enfeeblements", config.Debuffs, debuffLayout, scale)
        };
    }

    public string Name => "Player Statuses";

    public ModuleConfig Config => _config;

    public IEnumerable<IMovableModule> MovableParts => _editParts;

    public void Draw()
    {
        if (_actorProvider() is not { } player) return;

        if (_config.Permanent.Enabled) DrawGrid("SelUI_PlayerPermanent", _permanentLayout, _config.Permanent, player, true);
        if (_config.Buffs.Enabled) DrawGrid("SelUI_PlayerBuffs", _buffLayout, _config.Buffs, player, true);
        if (_config.Debuffs.Enabled) DrawGrid("SelUI_PlayerDebuffs", _debuffLayout, _config.Debuffs, player, false);
    }

    /// <summary>Apply the grid's user growth direction onto its baked layout, then draw it at full opacity.</summary>
    private void DrawGrid(string id, StatusListConfig layout, StatusSubConfig sub, IBattleChara player, bool buffs)
    {
        layout.GrowRight = sub.GrowRight;
        layout.GrowDown = sub.GrowDown;
        _statuses.Draw(id, layout, sub.Position, player, buffs, 1f);
    }

    public bool DrawConfig()
    {
        var changed = false;
        changed |= DrawSub("Statuses (Long Duration)", _config.Permanent);
        ImGui.Spacing();
        changed |= DrawSub("Enhancements", _config.Buffs);
        ImGui.Spacing();
        changed |= DrawSub("Enfeeblements", _config.Debuffs);

        // Grid look (icon size, columns, duration filter) is baked — see StatusLayouts.Player*.
        return changed;
    }

    public void Dispose()
    {
    }

    /// <summary>One status sub-grid's settings: on/off, position, and growth direction (the user state).</summary>
    private static bool DrawSub(string label, StatusSubConfig sub)
    {
        using var id = ImRaii.PushId(label);
        var changed = false;

        var enabled = sub.Enabled;
        if (ImGui.Checkbox(label, ref enabled))
        {
            sub.Enabled = enabled;
            changed = true;
        }

        using var disabled = ImRaii.Disabled(!sub.Enabled);

        var pos = sub.Position;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.DragFloat2("Position", ref pos))
        {
            sub.Position = pos;
            changed = true;
        }

        var growth = (int)sub.Growth;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.Combo("Growth", ref growth, GrowthItems, GrowthItems.Length))
        {
            sub.Growth = (StatusGrowth)growth;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    ///     An edit-mode handle for one status grid: a draggable box sized to a single full line of icons,
    ///     placed to match the grid's growth direction (it extends left of the anchor when the grid grows
    ///     leftward). Moving it shifts that grid's stored position. The box shows even when the sub-grid is
    ///     off, so it can be placed before enabling.
    /// </summary>
    private sealed class StatusEditBox : IMovableModule
    {
        private readonly StatusListConfig _layout;
        private readonly RenderScale _scale;
        private readonly StatusSubConfig _sub;

        public StatusEditBox(string label, StatusSubConfig sub, StatusListConfig layout, RenderScale scale)
        {
            EditLabel = label;
            _sub = sub;
            _layout = layout;
            _scale = scale;
        }

        public string EditLabel { get; }

        private float Step => (_layout.IconSize + StatusRenderer.Gap) * _scale.Value;
        private int Cols => Math.Max(1, Math.Min(_layout.MaxIcons, _layout.PerLine));

        // The first icon sits at Position; the box extends left of it when the grid grows leftward.
        public Vector2 EditTopLeft =>
            _sub.GrowRight ? _sub.Position : _sub.Position - new Vector2((Cols - 1) * Step, 0f);

        public Vector2 EditSize => new(Cols * Step - StatusRenderer.Gap * _scale.Value, _layout.IconSize * _scale.Value);

        public void MoveBy(Vector2 delta) => _sub.Position += delta;
    }
}
