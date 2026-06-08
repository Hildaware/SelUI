using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using SelUI.Game;
using SelUI.Modules.UnitFrames;
using SelUI.Rendering;

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

    private readonly PartyFramesConfig _config;
    private readonly UnitFrame _frame;
    private readonly IBuddyList _buddies;
    private readonly IObjectTable _objects;
    private readonly IPartyList _party;
    private readonly RenderScale _scale;
    // Distinct jobs shown in the preview (PLD, WHM, SCH, AST, BLM, SAM, BRD, WAR).
    private static readonly uint[] PreviewJobs = [19, 24, 28, 33, 25, 34, 23, 21];

    private readonly ITargetManager _targets;
    private readonly IReadOnlyList<uint> _mockBuffIcons;
    private readonly IReadOnlyList<uint> _mockDebuffIcons;
    private readonly Action<IGameObject> _onHover;
    private readonly Action<IGameObject> _onLeftClick;
    private readonly Action<IGameObject> _onRightClick;

    public PartyFrames(PartyFramesConfig config, IPartyList party, IBuddyList buddies, IObjectTable objects, ITargetManager targets, MouseoverManager mouseover, UnitFrame frame,
        IReadOnlyList<uint> mockBuffIcons, IReadOnlyList<uint> mockDebuffIcons, RenderScale scale)
    {
        _config = config;
        _party = party;
        _buddies = buddies;
        _objects = objects;
        _targets = targets;
        _frame = frame;
        _scale = scale;
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
        ? _config.Position - new Vector2(0f, (PreviewCount - 1) * RowPitch)
        : _config.Position;

    public Vector2 EditSize => new(_config.Row.Width * _scale.Value, RowPitch * PreviewCount);
    public void MoveBy(Vector2 delta) => _config.Position += delta;

    /// <summary>Vertical pitch between rows, scaled with the UI so spacing tracks the (scaled) row size.</summary>
    private float RowPitch => _config.RowHeight * _scale.Value;

    /// <summary>Top-left of row <paramref name="index" />, stacking up or down per config.</summary>
    private Vector2 RowOrigin(int index) => ListLayout.RowOrigin(_config.Position, _config.GrowUp, RowPitch, index);

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
                    leader: i == 0, leaderIconSize: LeaderIconSize * _scale.Value, leaderIconOffset: LeaderIconOffset * _scale.Value);
            }

            return;
        }

        var count = _party.Length;

        // Solo (not in a party): optionally show a single row for yourself, then your chocobo below it.
        if (count == 0)
        {
            var soloRow = 0;
            if (_config.ShowWhenSolo)
                DrawRow(soloRow++, _objects.LocalPlayer, false);
            DrawCompanionChocobo(soloRow);
            return;
        }

        var leaderIndex = _party.PartyLeaderIndex;

        // Always pin the local player to the top row, then draw everyone else in party order.
        var self = _objects.LocalPlayer;
        var selfIndex = -1;
        if (self != null)
            for (var i = 0; i < count; i++)
                if (_party[i]?.EntityId == self.EntityId)
                {
                    selfIndex = i;
                    break;
                }

        var row = 0;
        if (selfIndex >= 0)
            DrawMember(row++, _party[selfIndex]!, selfIndex == leaderIndex);
        for (var i = 0; i < count; i++)
        {
            if (i == selfIndex) continue;
            var member = _party[i];
            if (member == null) continue;
            DrawMember(row++, member, i == leaderIndex);
        }

        // The game lists your chocobo companion as an extra party member; mirror that with a row below the
        // real members.
        DrawCompanionChocobo(row);
    }

    /// <summary>
    ///     Draw one party member's row. Members in another zone (or beyond render range) have no live
    ///     GameObject; rather than let the frame fade out, fall back to the <see cref="IPartyMember" /> data via
    ///     the actor-less preview path — the same way alliance frames are driven. The shared range fade
    ///     (<see cref="UnitFrameConfig.RangeFade" />) dims those out of reach.
    /// </summary>
    private void DrawMember(int rowIndex, IPartyMember member, bool isLeader)
    {
        var readyCheck = ReadyCheck.IconFor(member.ContentId);
        if (member.GameObject is { } actor)
            DrawRow(rowIndex, actor, isLeader, readyCheckIcon: readyCheck);
        else
            DrawDistantRow(rowIndex, member, isLeader, readyCheck);
    }

    /// <summary>
    ///     Draw the local player's chocobo companion ("buddy") as a row at <paramref name="rowIndex" />, the way
    ///     the native party list does. No-op when no chocobo is summoned. The buddy isn't part of
    ///     <see cref="IPartyList" /> — it comes from <see cref="IBuddyList.CompanionBuddy" />.
    /// </summary>
    private void DrawCompanionChocobo(int rowIndex)
    {
        if (_buddies.CompanionBuddy is { GameObject: { } chocobo })
            DrawRow(rowIndex, chocobo, false, colorOverride: UnitColors.Chocobo, iconOverride: JobIcons.Chocobo);
    }

    /// <summary>
    ///     Draw a party member who has no live <see cref="IGameObject" /> (different zone / out of render
    ///     range) from their <see cref="IPartyMember" /> data, so the row stays visible (dimmed) instead of
    ///     vanishing. Not interactive — there's no actor to target.
    /// </summary>
    private void DrawDistantRow(int index, IPartyMember member, bool isLeader, uint readyCheckIcon = 0)
    {
        var maxHp = member.MaxHP;
        var maxMp = member.MaxMP;
        var unit = new PreviewUnit
        {
            Name = member.Name.TextValue,
            Level = member.Level,
            JobId = member.ClassJob.RowId,
            HpFraction = maxHp > 0 ? member.CurrentHP / (float)maxHp : 0f,
            MpFraction = maxMp > 0 ? member.CurrentMP / (float)maxMp : 0f
        };
        _frame.Draw($"SelUI_Party{index}", _config.Row, null, RowOrigin(index), preview: unit, rangePosition: member.Position,
            leader: isLeader, leaderIconSize: LeaderIconSize * _scale.Value, leaderIconOffset: LeaderIconOffset * _scale.Value,
            readyCheckIcon: readyCheckIcon);
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
        if (ImGui.Combo("Growth direction", ref grow, ListLayout.GrowthItems, ListLayout.GrowthItems.Length))
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

    private void DrawRow(int index, IGameObject? actor, bool isLeader, uint colorOverride = 0, uint iconOverride = 0, uint readyCheckIcon = 0)
    {
        var origin = RowOrigin(index);
        _frame.Draw($"SelUI_Party{index}", _config.Row, actor, origin, ActorState.IsSelected(_targets, actor), _onLeftClick, _onRightClick, _onHover,
            leader: isLeader, leaderIconSize: LeaderIconSize * _scale.Value, leaderIconOffset: LeaderIconOffset * _scale.Value,
            colorOverride: colorOverride, iconOverride: iconOverride, readyCheckIcon: readyCheckIcon);
    }
}
