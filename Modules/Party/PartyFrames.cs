using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using SelUI.Game;
using SelUI.Modules.UnitFrames;
using SelUI.Rendering;
using SelUI.UI;

namespace SelUI.Modules.Party;

/// <summary>
///     Party frames: one <see cref="UnitFrame" /> per party member, stacked vertically. Reuses the
///     shared unit-frame renderer and only adds party-specific bits (layout, solo handling, the party
///     leader crown).
/// </summary>
public sealed class PartyFrames : IHudModule
{
    private const uint LeaderIconId = 61521;

    private readonly PartyFramesConfig _config;
    private readonly UnitFrame _frame;
    private readonly IObjectTable _objects;
    private readonly IPartyList _party;
    // Distinct jobs shown in the preview (PLD, WHM, SCH, AST, BLM, SAM, BRD, WAR).
    private static readonly uint[] PreviewJobs = [19, 24, 28, 33, 25, 34, 23, 21];

    private readonly ITargetManager _targets;
    private readonly ITextureProvider _textures;
    private readonly IReadOnlyList<uint> _mockBuffIcons;
    private readonly IReadOnlyList<uint> _mockDebuffIcons;
    private readonly Action<IGameObject> _onHover;
    private readonly Action<IGameObject> _onLeftClick;
    private readonly Action<IGameObject> _onRightClick;
    private ISharedImmediateTexture? _leaderIcon;

    public PartyFrames(PartyFramesConfig config, IPartyList party, IObjectTable objects, ITargetManager targets, MouseoverManager mouseover, UnitFrame frame, ITextureProvider textures,
        IReadOnlyList<uint> mockBuffIcons, IReadOnlyList<uint> mockDebuffIcons)
    {
        _config = config;
        _party = party;
        _objects = objects;
        _targets = targets;
        _frame = frame;
        _textures = textures;
        _mockBuffIcons = mockBuffIcons;
        _mockDebuffIcons = mockDebuffIcons;
        _onLeftClick = actor => _targets.Target = actor;
        _onRightClick = UnitInteraction.OpenContextMenu;
        _onHover = mouseover.SetHovered;
    }

    public string Name => "Party Frames";

    public ModuleConfig Config => _config;

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
                var origin = _config.Position + new Vector2(0f, i * _config.RowHeight);
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
                _frame.Draw($"SelUI_Party{i}", _config.Row, null, origin, false, preview: unit);

                if (i == 0 && _config.ShowLeaderIcon)
                    DrawLeaderIcon(i, origin);
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

        var leader = _config.ShowLeaderIcon;
        if (ImGui.Checkbox("Show leader icon", ref leader))
        {
            _config.ShowLeaderIcon = leader;
            changed = true;
        }

        if (_config.ShowLeaderIcon)
        {
            var size = _config.LeaderIconSize;
            if (ImGui.DragFloat("Leader icon size", ref size, 0.5f, 8f, 64f, "%.0f"))
            {
                _config.LeaderIconSize = size;
                changed = true;
            }

            var offset = _config.LeaderIconOffset;
            if (ImGui.DragFloat2("Leader icon offset", ref offset))
            {
                _config.LeaderIconOffset = offset;
                changed = true;
            }
        }

        if (ImGui.CollapsingHeader("Row style"))
        {
            using var indent = ImRaii.PushIndent();
            changed |= UnitFrameConfigUI.Draw(_config.Row);
        }

        return changed;
    }

    private void DrawRow(int index, IGameObject? actor, bool isLeader)
    {
        var origin = _config.Position + new Vector2(0f, index * _config.RowHeight);
        _frame.Draw($"SelUI_Party{index}", _config.Row, actor, origin, IsSelected(actor), _onLeftClick, _onRightClick, _onHover);

        if (isLeader && _config.ShowLeaderIcon)
            DrawLeaderIcon(index, origin);
    }

    /// <summary>Whether this actor is the player's current target (hard or gamepad soft target).</summary>
    private bool IsSelected(IGameObject? actor)
    {
        if (actor == null) return false;
        return (_targets.Target != null && _targets.Target.Address == actor.Address)
               || (_targets.SoftTarget != null && _targets.SoftTarget.Address == actor.Address);
    }

    private void DrawLeaderIcon(int index, Vector2 rowOrigin)
    {
        _leaderIcon ??= _textures.GetFromGameIcon(new GameIconLookup(LeaderIconId));
        var wrap = _leaderIcon.GetWrapOrEmpty();

        var size = new Vector2(_config.LeaderIconSize);
        var pos = rowOrigin + _config.LeaderIconOffset;
        DrawHelper.DrawInWindow($"SelUI_PartyLeader{index}", pos, size, false,
            dl => dl.AddImage(wrap.Handle, pos, pos + size));
    }
}
