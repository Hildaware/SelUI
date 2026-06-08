using Newtonsoft.Json;
using SelUI.Rendering;

namespace SelUI.Modules.UnitFrames;

/// <summary>How the health bar's overlaid text reads.</summary>
public enum HealthTextMode
{
    None,
    Value,
    Percent,
    ValueAndPercent
}

/// <summary>
///     The shape of every unit frame in SelUI. Each of the seven primitives — health bar, mana bar,
///     cast bar, name, level, job icon, health text — is independently toggleable. Player, target,
///     party members, the enemy list and nameplates are all just this config with different defaults
///     and a different actor source.
/// </summary>
public sealed class UnitFrameConfig : ModuleConfig
{
    /// <summary>Top-left screen position of the bars (the job icon, if shown, sits to the left of this).</summary>
    public Vector2 Position { get; set; } = new(80f, 800f);

    public float Width { get; set; } = 220f;

    /// <summary>When true the frame hides if there is no actor (used by target / focus). Player keeps this false.</summary>
    public bool HideWhenNoActor { get; set; }

    /// <summary>Hide the frame while the player is in combat (only wired for the target frame).</summary>
    public bool HideInCombat { get; set; }

    /// <summary>
    ///     Fade the frame out — and stop it taking clicks — once the actor is past action range (~30y).
    ///     Opt-in per frame (party / alliance rows); the player frame, nameplates, etc. leave it off.
    /// </summary>
    public bool RangeFade { get; set; }

    public float FontSize { get; set; } = 14f;
    public float Gap { get; set; } = 2f;

    // Health bar — fill color comes from the actor (job color for players, disposition for NPCs).
    public bool ShowHealthBar { get; set; } = true;
    public float HealthBarHeight { get; set; } = 22f;
    public HealthTextMode HealthText { get; set; } = HealthTextMode.Value;

    /// <summary>Draw the health text on the left of the bar instead of the right.</summary>
    public bool HealthTextOnLeft { get; set; }

    /// <summary>Vertical nudge of the health text from its centered position (positive = down). Baked per-frame.</summary>
    public float HealthTextOffsetY { get; set; }

    // Mana bar
    public bool ShowManaBar { get; set; } = true;

    /// <summary>When set, the mana bar only shows for healer jobs (every other job has full but irrelevant MP).</summary>
    public bool ManaHealersOnly { get; set; }

    public float ManaBarHeight { get; set; } = 12f;
    public uint ManaColor { get; set; } = Colors.Mp;

    /// <summary>Mana bar width as a fraction of the frame width.</summary>
    public float ManaWidthFactor { get; set; } = 1f;

    /// <summary>Overlap the mana bar onto the bottom-right of the health bar instead of stacking below it.</summary>
    public bool ManaOverlapHealth { get; set; }

    // Cast bar
    public bool ShowCastBar { get; set; } = true;
    public float CastBarHeight { get; set; } = 16f;
    public bool ShowCastName { get; set; } = true;
    public bool ShowCastTime { get; set; } = true;

    /// <summary>Overlap the cast bar onto the bottom-right of the health bar (like the party mana bar) instead of stacking below it. Suppresses the cast name/time text.</summary>
    public bool CastOverlapHealth { get; set; }

    /// <summary>Cast bar width as a fraction of the frame width when <see cref="CastOverlapHealth" /> is set.</summary>
    public float CastWidthFactor { get; set; } = 1f;

    public uint CastColor { get; set; } = Colors.FromHex("D9A441");
    public uint CastInterruptibleColor { get; set; } = Colors.FromHex("E05A5A");

    // Name / level
    public bool ShowName { get; set; } = true;
    public bool ShowLevel { get; set; } = true;
    public bool NameCentered { get; set; }

    /// <summary>Color the name by the actor's color (job color for players) instead of <see cref="TextColor" />.</summary>
    public bool NameUseJobColor { get; set; }

    /// <summary>Place the name to the right of the job icon (left-aligned) instead of above the bar.</summary>
    public bool NameRightOfIcon { get; set; }

    /// <summary>Dock the job icon's right edge to the left of a centered name.</summary>
    public bool JobIconLeftOfName { get; set; }

    /// <summary>Vertically center the (left-aligned) name on the bar's top edge instead of sitting above it.</summary>
    public bool NameOnBarLine { get; set; }

    /// <summary>Gap between the job icon's right edge and the name when <see cref="NameRightOfIcon" /> is set (negative tucks the name into the icon).</summary>
    public float NameRightOfIconGap { get; set; } = 6f;

    public float NameFontSize { get; set; } = 26f;
    public float LevelFontSize { get; set; } = 26f;

    /// <summary>Vertical nudge of the name from its computed baseline (negative = up). Baked per-frame.</summary>
    public float NameOffsetY { get; set; }

    /// <summary>Draw a highlight texture behind a centered name (used by name-only nameplates to lift the text off the world). Baked per-frame.</summary>
    public bool NameBackground { get; set; }

    // Job icon
    public bool ShowJobIcon { get; set; } = true;
    public float JobIconSize { get; set; } = 54f;

    /// <summary>Horizontal nudge of the job icon from its straddle position on the bar's left edge.</summary>
    public float JobIconOffsetX { get; set; }

    /// <summary>Vertical nudge of the job icon (screen px, negative = up). Used by name-less icon plates.</summary>
    public float JobIconOffsetY { get; set; }

    /// <summary>Vertical anchor of the icon's center within the health bar: 0 = top edge, 0.5 = center, 1 = bottom.</summary>
    public float JobIconAnchorY { get; set; } = 0.5f;

    // Status effects, split into independently-positioned collections. Their layout is baked design,
    // not user config, so it isn't serialized; the real per-frame values come from StatusLayouts and
    // are applied on every load (see Plugin).
    [JsonIgnore] public StatusListConfig Buffs { get; set; } = new();
    [JsonIgnore] public StatusListConfig Debuffs { get; set; } = new();

    /// <summary>
    ///     Draw buffs and debuffs as a single continuous grid (debuffs first, then buffs) using the
    ///     <see cref="Debuffs" /> collection's layout. The <see cref="Buffs" /> layout is ignored; only its
    ///     filters (e.g. own-only) still apply. Baked per-frame — see <see cref="PartyRowDefault" />.
    /// </summary>
    public bool CombineStatuses { get; set; }

    // Shared appearance
    public uint BackgroundColor { get; set; } = Colors.BarBackground;
    public uint BorderColor { get; set; } = Colors.BarBorder;
    public uint TextColor { get; set; } = Colors.White;
    public uint TitleColor { get; set; } = Colors.White;

    /// <summary>
    ///     A copy with the two status collections deep-copied, so a caller can mutate dimensions (e.g.
    ///     distance-scaling a nameplate) without touching the shared baked layout it was cloned from.
    /// </summary>
    public UnitFrameConfig Clone()
    {
        var c = (UnitFrameConfig)MemberwiseClone();
        c.Buffs = Buffs.Clone();
        c.Debuffs = Debuffs.Clone();
        return c;
    }

    /// <summary>Sensible defaults for the player's own frame (lower-left, always visible).</summary>
    public static UnitFrameConfig PlayerDefault()
    {
        return new UnitFrameConfig
        {
            Position = new Vector2(80f, 800f),
            ShowCastBar = false, // the player cast bar is its own module (PlayerCastBar)
            HideWhenNoActor = false
        };
    }

    /// <summary>
    ///     Compact defaults for a party-list row. The party frame exposes almost no row-level config — these
    ///     values are baked here and re-applied on every load, so tuning a party row means editing this method.
    /// </summary>
    public static UnitFrameConfig PartyRowDefault()
    {
        return new UnitFrameConfig
        {
            Width = 240f,
            HealthBarHeight = 20f,
            HealthText = HealthTextMode.None,
            ManaBarHeight = 8f,
            ManaWidthFactor = 0.75f,
            ManaOverlapHealth = true,
            ManaHealersOnly = true, // non-healers' MP is irrelevant — only show it for healers
            ShowCastBar = false,
            ShowLevel = false,
            NameCentered = false,
            NameRightOfIcon = true,
            NameRightOfIconGap = -6f,
            NameFontSize = 20f,
            NameOffsetY = -2f,
            JobIconSize = 48f,
            JobIconOffsetX = 4f,
            JobIconAnchorY = 0f,
            HideWhenNoActor = false,
            RangeFade = true, // dim party members you can't reach (heals/buffs ~30y)
            CombineStatuses = true // debuffs then buffs in one grid, to the right of the bar
        };
    }

    /// <summary>Compact defaults for an enemy-list row: HP only, no job icon / mana / cast.</summary>
    public static UnitFrameConfig EnemyRowDefault()
    {
        return new UnitFrameConfig
        {
            Width = 220f,
            HealthBarHeight = 20f,
            ShowManaBar = false,
            ShowCastBar = false,
            ShowJobIcon = false,
            ShowLevel = false,
            NameCentered = false,
            NameOnBarLine = true,
            NameFontSize = 16f,
            FontSize = 12f,
            HealthText = HealthTextMode.Percent,
            HealthTextOnLeft = true,
            HealthTextOffsetY = 4f,
            HideWhenNoActor = false
        };
    }

    /// <summary>
    ///     Defaults for the target frame (upper area, hidden when nothing is targeted). Appearance is baked
    ///     and re-applied on every load (see Plugin); only Position, Width and HideInCombat stay user state.
    /// </summary>
    public static UnitFrameConfig TargetDefault()
    {
        return new UnitFrameConfig
        {
            Position = new Vector2(760f, 120f),
            Width = 600f,
            HealthBarHeight = 16f,
            HealthText = HealthTextMode.None,
            ShowManaBar = false,
            ShowCastBar = false,
            JobIconSize = 62f,
            HideWhenNoActor = true,
            HideInCombat = true,
            NameCentered = true
        };
    }

    /// <summary>
    ///     Compact defaults for an alliance-list row: a small job-colored health bar with the name and job
    ///     icon docked top-left, like a party row but scaled down. HP only — no mana / cast / level / text.
    ///     Baked design, built fresh by the alliance module (not persisted).
    /// </summary>
    public static UnitFrameConfig AllianceRowDefault()
    {
        return new UnitFrameConfig
        {
            Width = 140f,
            HealthBarHeight = 12f,
            HealthText = HealthTextMode.None,
            ShowManaBar = false,
            ShowCastBar = false,
            ShowLevel = false,
            NameRightOfIcon = true,
            NameRightOfIconGap = -4f,
            NameFontSize = 14f,
            NameOffsetY = -1f,
            JobIconSize = 28f,
            JobIconOffsetX = 4f,
            JobIconAnchorY = 0f,
            HideWhenNoActor = false,
            RangeFade = true // dim alliance members you can't reach (heals/rez ~30y)
        };
    }
}
