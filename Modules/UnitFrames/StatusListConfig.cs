namespace SelUI.Modules.UnitFrames;

/// <summary>How a status grid filters by remaining time, to split long "permanent" statuses from short ones.</summary>
public enum DurationFilter
{
    /// <summary>No duration filter — show every status in the category.</summary>
    Any,

    /// <summary>Only long / timerless statuses (effective remaining over the long-status threshold).</summary>
    LongOnly,

    /// <summary>Only short statuses (effective remaining at/under the threshold); excludes timerless ones.</summary>
    ShortOnly
}

/// <summary>
///     One collection of status icons (either buffs or debuffs) attached to a unit frame. Position is
///     an offset from the frame's origin so the collection can be placed independently.
/// </summary>
public sealed class StatusListConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Offset from the frame origin to the top-left of the first icon.</summary>
    public Vector2 Position { get; set; }

    public float IconSize { get; set; } = 28f;
    public int MaxIcons { get; set; } = 16;
    public int PerLine { get; set; } = 8;

    /// <summary>Horizontal growth direction within a line.</summary>
    public bool GrowRight { get; set; } = true;

    /// <summary>Vertical growth direction across wrapped lines.</summary>
    public bool GrowDown { get; set; } = true;

    public bool ShowDuration { get; set; } = true;
    public bool ShowStacks { get; set; } = true;
    public float FontSize { get; set; } = 12f;

    /// <summary>Crop the framed/directional border off the status icon, showing just the clean inner art.</summary>
    public bool CropIcon { get; set; } = true;

    /// <summary>Only show statuses applied by the local player (default for debuffs).</summary>
    public bool OwnOnly { get; set; }

    /// <summary>Only show statuses that can be dispelled / cleansed.</summary>
    public bool CleansableOnly { get; set; }

    /// <summary>Remaining-time partition: splits long "permanent" buffs (food, FC, etc.) from short combat buffs.</summary>
    public DurationFilter Duration { get; set; } = DurationFilter.Any;

    /// <summary>Shallow copy (all members are value types). Used when distance-scaling a frame.</summary>
    public StatusListConfig Clone()
    {
        return (StatusListConfig)MemberwiseClone();
    }
}
