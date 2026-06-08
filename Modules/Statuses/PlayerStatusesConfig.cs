using Newtonsoft.Json;

namespace SelUI.Modules.Statuses;

/// <summary>The direction a status grid flows from its anchor (first icon): horizontal flow, then wrap.</summary>
public enum StatusGrowth
{
    RightDown,
    LeftDown,
    RightUp,
    LeftUp
}

/// <summary>
///     The player's own buff/debuff grids, broken out of the unit frame into a standalone HUD element
///     (the way the cast bar is). One module with three independently-placed sub-grids — permanent (long)
///     buffs, regular (short) buffs, and debuffs; only each grid's position, growth direction and on/off
///     toggle are user state — the grid look (icon size, columns, duration filter) is baked
///     (<see cref="UnitFrames.StatusLayouts" />) and applied on load.
/// </summary>
public sealed class PlayerStatusesConfig : ModuleConfig
{
    /// <summary>Long-running "permanent" buffs (food, FC, etc. — over 5 min). Sits above the regular buffs by default.</summary>
    public StatusSubConfig Permanent { get; set; } = new() { Position = new Vector2(80f, 680f) };

    /// <summary>Regular (short) buffs on the player. Sits a little above the player frame by default.</summary>
    public StatusSubConfig Buffs { get; set; } = new() { Position = new Vector2(80f, 720f) };

    /// <summary>Debuffs on the player (mostly from enemies). Sits below the player frame by default.</summary>
    public StatusSubConfig Debuffs { get; set; } = new() { Position = new Vector2(80f, 870f) };
}

/// <summary>One status grid's user state: where it sits, whether it draws, and which way it grows.</summary>
public sealed class StatusSubConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Top-left screen position of the first status icon.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Which way the grid flows from the first icon (horizontal direction, then wrap direction).</summary>
    public StatusGrowth Growth { get; set; } = StatusGrowth.RightDown;

    /// <summary>Horizontal flow direction from <see cref="Growth" /> (true = rightward). Derived; not serialized.</summary>
    [JsonIgnore] public bool GrowRight => Growth is StatusGrowth.RightDown or StatusGrowth.RightUp;

    /// <summary>Vertical wrap direction from <see cref="Growth" /> (true = downward). Derived; not serialized.</summary>
    [JsonIgnore] public bool GrowDown => Growth is StatusGrowth.RightDown or StatusGrowth.LeftDown;
}
