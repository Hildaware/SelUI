using SelUI.Modules.UnitFrames;
using SelUI.Rendering;

namespace SelUI.Modules.Nameplates;

/// <summary>
///     Baked per-type nameplate layouts (design constants, not user config). Only enemies show a health
///     bar; enemies further have idle / in-combat / targeted states. Edit here + rebuild to change them.
/// </summary>
public static class NameplateLayouts
{
    private const float TargetScale = 1.25f; // enemy bar scales up while it's the player's target

    // Enemy states.
    public static readonly UnitFrameConfig EnemyIdle = NameOnlyHighlighted(); // out of combat: name only, on a highlight
    public static readonly UnitFrameConfig EnemyCombat = EnemyConfig(1f, showName: false); // non-target, overworld: bars only (cut clutter)
    public static readonly UnitFrameConfig EnemyCombatNamed = EnemyConfig(1f, showName: true); // non-target, in a duty: keep the name
    public static readonly UnitFrameConfig EnemyTarget = EnemyConfig(TargetScale, showName: true); // targeted: bigger, keeps its name

    // Party member in combat (opt-in): a single enlarged job icon, no name or bars.
    public static readonly UnitFrameConfig PartyCombatIcon = PartyCombat();

    private static readonly Dictionary<NameplateType, UnitFrameConfig> Configs = Build();

    public static UnitFrameConfig For(NameplateType type)
    {
        return Configs[type];
    }

    // "Name only mode": the no-health-bar layout, used by every non-enemy type and idle enemies.
    private static UnitFrameConfig Plate()
    {
        return new UnitFrameConfig
        {
            Width = 200f,
            HealthBarHeight = 14f,
            ShowHealthBar = false,
            ShowManaBar = false,
            ShowCastBar = false,
            ShowJobIcon = false,
            ShowLevel = false,
            NameCentered = true,
            NameFontSize = 24f,
            HealthText = HealthTextMode.None,
            Buffs = new StatusListConfig { Enabled = false },
            Debuffs = new StatusListConfig { Enabled = false }
        };
    }

    // A name-only plate with the highlight texture behind the name (Object, NPC, idle Enemy).
    private static UnitFrameConfig NameOnlyHighlighted()
    {
        var c = Plate();
        c.NameBackground = true;
        return c;
    }

    private static UnitFrameConfig EnemyConfig(float scale, bool showName)
    {
        var c = Plate();
        c.ShowHealthBar = true;
        c.ShowCastBar = true;
        c.ShowName = showName;
        c.Width = 260f * scale;
        c.HealthBarHeight = 18f * scale;
        c.NameFontSize = 16f * scale;
        // Cast bar styled like the party mana bar: a thin strip overlapping the health bar's bottom-right.
        c.CastBarHeight = 8f * scale;
        c.CastOverlapHealth = true;
        c.CastWidthFactor = 0.75f;
        var nameH = showName ? c.NameFontSize : 0f; // the name reserves a header row above the bar
        c.Debuffs = new StatusListConfig
        {
            OwnOnly = true,
            Position = new Vector2(0f, nameH + c.HealthBarHeight + 4f),
            IconSize = 20f * scale,
            FontSize = 10f * scale
        };
        return c;
    }

    // Centered, enlarged job icon. The icon center is origin.X + JobIconOffsetX and origin.X is
    // screen.X - Width/2, so Width == JobIconSize and JobIconOffsetX == Width/2 land it on the node.
    private static UnitFrameConfig PartyCombat()
    {
        var c = Plate();
        c.ShowName = false;
        c.ShowJobIcon = true;
        c.JobIconSize = 56f;
        c.Width = 56f;
        c.JobIconOffsetX = 28f; // = Width/2, centers the icon on the nameplate point
        return c;
    }

    private static Dictionary<NameplateType, UnitFrameConfig> Build()
    {
        // Players (and party / alliance / friends): job-colored name, job icon docked left of the name,
        // yellow-orange title.
        UnitFrameConfig PlayerStyle()
        {
            var c = Plate();
            c.ShowJobIcon = true;
            c.NameUseJobColor = true;
            c.JobIconLeftOfName = true;
            c.JobIconOffsetX = 10f;
            c.TitleColor = Colors.FromHex("FFB347"); // yellow-orange
            return c;
        }

        var player = PlayerStyle();
        var party = PlayerStyle();
        var alliance = PlayerStyle();
        var friend = PlayerStyle();

        var lightBlue = Colors.FromHex("88CCFF");
        var pet = Plate();
        pet.TextColor = lightBlue;
        pet.TitleColor = lightBlue;
        var minion = Plate();
        minion.TextColor = lightBlue;
        minion.TitleColor = lightBlue;
        var npc = NameOnlyHighlighted();
        npc.TextColor = Colors.FromHex("88E68C"); // light green

        var obj = NameOnlyHighlighted();
        obj.Enabled = false;

        return new Dictionary<NameplateType, UnitFrameConfig>
        {
            [NameplateType.Enemy] = EnemyCombat, // fallback; the module picks the enemy state
            [NameplateType.Player] = player,
            [NameplateType.PartyMember] = party,
            [NameplateType.AllianceMember] = alliance,
            [NameplateType.Friend] = friend,
            [NameplateType.Pet] = pet,
            [NameplateType.Npc] = npc,
            [NameplateType.Minion] = minion,
            [NameplateType.Object] = obj
        };
    }
}
