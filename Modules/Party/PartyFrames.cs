using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using SelUI.Game;
using SelUI.Modules.UnitFrames;

namespace SelUI.Modules.Party;

/// <summary>
///     Party frames: one <see cref="UnitFrame" /> per party member, stacked vertically. Reuses the
///     shared unit-frame renderer and only adds party-specific bits (layout, solo handling, the party
///     leader crown).
/// </summary>
public sealed class PartyFrames : IHudModule, IMovableModule
{
    // Leader crown — baked appearance (drawn on top by UnitFrame, see UnitFrameConfig leader params).
    private const float LeaderIconSize = 20f;
    private static readonly Vector2 LeaderIconOffset = new(-8f, 8f);
    private static readonly string[] GrowthItems = ["Down", "Up"]; // index 0 = down, 1 = up (matches GrowUp)

    private readonly PartyFramesConfig _config;
    private readonly UnitFrame _frame;
    private readonly IObjectTable _objects;
    private readonly IPartyList _party;
    // Distinct jobs shown in the preview (PLD, WHM, SCH, AST, BLM, SAM, BRD, WAR).
    private static readonly uint[] PreviewJobs = [19, 24, 28, 33, 25, 34, 23, 21];

    private readonly ITargetManager _targets;
    private readonly IReadOnlyList<uint> _mockBuffIcons;
    private readonly IReadOnlyList<uint> _mockDebuffIcons;
    private readonly Action<IGameObject> _onHover;
    private readonly Action<IGameObject> _onLeftClick;
    private readonly Action<IGameObject> _onRightClick;

    public PartyFrames(PartyFramesConfig config, IPartyList party, IObjectTable objects, ITargetManager targets, MouseoverManager mouseover, UnitFrame frame,
        IReadOnlyList<uint> mockBuffIcons, IReadOnlyList<uint> mockDebuffIcons)
    {
        _config = config;
        _party = party;
        _objects = objects;
        _targets = targets;
        _frame = frame;
        _mockBuffIcons = mockBuffIcons;
        _mockDebuffIcons = mockDebuffIcons;
        _onLeftClick = actor => _targets.Target = actor;
        _onRightClick = UnitInteraction.OpenContextMenu;
        _onHover = mouseover.SetHovered;
    }

    public string Name => "Party Frames";

    public ModuleConfig Config => _config;

    public string EditLabel => Name;

    public Vector2 EditTopLeft => _config.GrowUp
        ? _config.Position - new Vector2(0f, (PreviewCount - 1) * _config.RowHeight)
        : _config.Position;

    public Vector2 EditSize => new(_config.Row.Width, _config.RowHeight * PreviewCount);
    public void MoveBy(Vector2 delta) => _config.Position += delta;

    /// <summary>Top-left of row <paramref name="index" />, stacking up or down per config.</summary>
    private Vector2 RowOrigin(int index) =>
        _config.Position + new Vector2(0f, (_config.GrowUp ? -1f : 1f) * index * _config.RowHeight);

    public void Dispose()
    {
    }

    private const int PreviewCount = 8;

    public void Draw()
    {
        // Preview: a full party of distinct jobs with mock buffs/debuffs, for positioning/styling.
        if (_config.PreviewMode)
        {
            for (var i = 0; i < PreviewCount; i++)
            {
                var origin = RowOrigin(i);
                var unit = new PreviewUnit
                {
                    Name = $"Player {i + 1}",
                    Level = 100,
                    JobId = PreviewJobs[i % PreviewJobs.Length],
                    HpFraction = 1f - i * 0.08f,
                    MpFraction = 1f - i * 0.05f,
                    BuffIcons = _mockBuffIcons,
                    DebuffIcons = _mockDebuffIcons
                };
                // The last two rows preview the out-of-range dim (see UnitFrameConfig.RangeFade); the
                // first row previews the leader crown.
                var outOfRange = i >= PreviewCount - 2;
                _frame.Draw($"SelUI_Party{i}", _config.Row, null, origin, false, preview: unit,
                    alphaMultiplier: outOfRange ? UnitFrame.OutOfRangeAlpha : 1f,
                    leader: i == 0, leaderIconSize: LeaderIconSize, leaderIconOffset: LeaderIconOffset);
            }

            return;
        }

        var count = _party.Length;

        // Solo (not in a party): optionally show a single row for yourself.
        if (count == 0)
        {
            if (_config.ShowWhenSolo)
                DrawRow(0, _objects.LocalPlayer, false);
            return;
        }

        var leaderIndex = _party.PartyLeaderIndex;
        for (var i = 0; i < count; i++)
        {
            var member = _party[i];
            if (member == null) continue;
            DrawRow(i, member.GameObject, i == leaderIndex);
        }
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

        var rowHeight = _config.RowHeight;
        if (ImGui.DragFloat("Frame spacing", ref rowHeight, 0.5f, 10f, 200f, "%.0f"))
        {
            _config.RowHeight = rowHeight;
            changed = true;
        }

        var grow = _config.GrowUp ? 1 : 0;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.Combo("Growth direction", ref grow, GrowthItems, GrowthItems.Length))
        {
            _config.GrowUp = grow == 1;
            changed = true;
        }

        var solo = _config.ShowWhenSolo;
        if (ImGui.Checkbox("Show when solo", ref solo))
        {
            _config.ShowWhenSolo = solo;
            changed = true;
        }

        var preview = _config.PreviewMode;
        if (ImGui.Checkbox("Show preview", ref preview))
        {
            _config.PreviewMode = preview;
            changed = true;
        }

        // Row appearance (bars, name, job icon, buffs/debuffs) and the leader crown are baked, not
        // user-configurable — see UnitFrameConfig.PartyRowDefault and StatusLayouts.Party*.

        return changed;
    }

    private void DrawRow(int index, IGameObject? actor, bool isLeader)
    {
        var origin = RowOrigin(index);
        _frame.Draw($"SelUI_Party{index}", _config.Row, actor, origin, IsSelected(actor), _onLeftClick, _onRightClick, _onHover,
            leader: isLeader, leaderIconSize: LeaderIconSize, leaderIconOffset: LeaderIconOffset);
    }

    /// <summary>Whether this actor is the player's current target (hard or gamepad soft target).</summary>
    private bool IsSelected(IGameObject? actor)
    {
        if (actor == null) return false;
        return (_targets.Target != null && _targets.Target.Address == actor.Address)
               || (_targets.SoftTarget != null && _targets.SoftTarget.Address == actor.Address);
    }
}
