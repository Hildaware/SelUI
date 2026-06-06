using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using SelUI.Modules.UnitFrames;

namespace SelUI.Modules.Alliance;

/// <summary>
///     Alliance frames: the two *other* alliances in a 24-man, drawn as two columns of eight compact
///     <see cref="UnitFrame" /> rows. Your own party is already covered by the party frames, so this only
///     renders the alliance list. Alliance members are usually outside actor render range, so each row is
///     driven from the <see cref="Dalamud.Game.ClientState.Party.IPartyMember" /> data (name / job / HP) via the actor-less
///     <see cref="PreviewUnit" /> path rather than a live <see cref="Dalamud.Game.ClientState.Objects.Types.IGameObject" />.
/// </summary>
public sealed unsafe class AllianceFrames : IHudModule, IMovableModule
{
    private const int MaxMembers = 16;       // two other alliances of eight
    private const int RowsPerColumn = 8;
    private const float RowHeight = 30f;     // vertical pitch between rows
    private const float ColumnWidth = 170f;  // horizontal pitch between the two columns

    // Leader crown — baked appearance (drawn on top by UnitFrame), sized for the compact alliance row.
    private const float LeaderIconSize = 16f;
    private static readonly Vector2 LeaderIconOffset = new(-8f, -2f);

    // Distinct jobs for the preview's two columns, so the styling reads clearly while positioning.
    private static readonly uint[] PreviewJobs = [19, 24, 28, 33, 25, 34, 23, 21, 32, 22, 30, 31, 20, 37, 38, 41];

    private readonly AllianceFramesConfig _config;
    private readonly UnitFrame _frame;
    private readonly IPartyList _party;
    private readonly UnitFrameConfig _row = UnitFrameConfig.AllianceRowDefault();

    public AllianceFrames(AllianceFramesConfig config, IPartyList party, UnitFrame frame)
    {
        _config = config;
        _party = party;
        _frame = frame;
    }

    public string Name => "Alliance Frames";

    public ModuleConfig Config => _config;

    public string EditLabel => Name;

    public Vector2 EditTopLeft => _config.Position;

    public Vector2 EditSize => new(ColumnWidth + _row.Width, RowHeight * RowsPerColumn);

    public void MoveBy(Vector2 delta) => _config.Position += delta;

    public void Dispose()
    {
    }

    /// <summary>Top-left of member <paramref name="index" />: column 0 = members 0–7, column 1 = 8–15.</summary>
    private Vector2 RowOrigin(int index) =>
        _config.Position + new Vector2(index / RowsPerColumn * ColumnWidth, index % RowsPerColumn * RowHeight);

    public void Draw()
    {
        // Preview: two full alliances of distinct jobs for positioning/styling.
        if (_config.PreviewMode)
        {
            for (var i = 0; i < MaxMembers; i++)
            {
                var unit = new PreviewUnit
                {
                    Name = $"Player {i + 1}",
                    JobId = PreviewJobs[i % PreviewJobs.Length],
                    HpFraction = 1f - i % RowsPerColumn * 0.05f
                };
                // The bottom two of each column preview the out-of-range dim (see UnitFrameConfig.RangeFade);
                // the first member of each alliance previews the leader crown.
                var outOfRange = i % RowsPerColumn >= RowsPerColumn - 2;
                _frame.Draw($"SelUI_Alliance{i}", _row, null, RowOrigin(i), preview: unit,
                    alphaMultiplier: outOfRange ? UnitFrame.OutOfRangeAlpha : 1f,
                    leader: i % RowsPerColumn == 0, leaderIconSize: LeaderIconSize, leaderIconOffset: LeaderIconOffset);
            }

            return;
        }

        if (!_party.IsAlliance) return;

        for (var i = 0; i < MaxMembers; i++)
        {
            var addr = _party.GetAllianceMemberAddress(i);
            if (addr == nint.Zero) continue;

            var member = _party.CreateAllianceMemberReference(addr);
            if (member == null) continue;

            var maxHp = member.MaxHP;
            var unit = new PreviewUnit
            {
                Name = member.Name.TextValue,
                JobId = member.ClassJob.RowId,
                HpFraction = maxHp > 0 ? member.CurrentHP / (float)maxHp : 0f
            };
            // Alliance members are usually out of action range; feed their party-list position so the
            // shared range fade (UnitFrameConfig.RangeFade) dims those you can't reach.
            _frame.Draw($"SelUI_Alliance{i}", _row, null, RowOrigin(i), preview: unit, rangePosition: member.Position,
                leader: IsPartyLeader(member.EntityId), leaderIconSize: LeaderIconSize, leaderIconOffset: LeaderIconOffset);
        }
    }

    /// <summary>
    ///     Whether <paramref name="entityId" /> leads its (alliance) party, per the game's own group data.
    ///     Returns false for anyone the engine doesn't flag, so a wrong row is never crowned.
    /// </summary>
    private static bool IsPartyLeader(uint entityId)
    {
        if (entityId is 0 or 0xE0000000) return false;
        var gm = GroupManager.Instance();
        return gm != null && gm->MainGroup.IsEntityIdPartyLeader(entityId);
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

        var preview = _config.PreviewMode;
        if (ImGui.Checkbox("Show preview", ref preview))
        {
            _config.PreviewMode = preview;
            changed = true;
        }

        // Row appearance (bar size, name, job icon) is baked — see UnitFrameConfig.AllianceRowDefault.

        return changed;
    }
}
