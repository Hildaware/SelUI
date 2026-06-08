namespace SelUI.Modules.UnitFrames;

/// <summary>
///     Baked, per-frame status-collection layouts. These are design constants, not user config — they
///     are applied to the live config on every load (see Plugin), so editing them here + rebuilding is
///     the way to move/resize status collections. Offsets are relative to the frame origin.
/// </summary>
public static class StatusLayouts
{
    // --- Player ---
    public static StatusListConfig PlayerBuffs()
    {
        return new StatusListConfig { Position = new Vector2(0f, -72f) };
    }

    public static StatusListConfig PlayerDebuffs()
    {
        // Debuffs on yourself come from enemies, so don't restrict to "mine".
        return new StatusListConfig { Position = new Vector2(0f, 54f) };
    }

    // --- Target ---
    public static StatusListConfig TargetBuffs()
    {
        // Only my buffs, anchored to the bottom-right of the 600-wide health bar, growing left.
        return new StatusListConfig
        {
            OwnOnly = true,
            Position = new Vector2(600f - 28f, 54f),
            GrowRight = false
        };
    }

    public static StatusListConfig TargetDebuffs()
    {
        // Only debuffs you applied, bottom-left of the health bar, growing right.
        return new StatusListConfig { Position = new Vector2(0f, 54f), OwnOnly = true };
    }

    // --- Party row ---
    // The party row uses UnitFrameConfig.CombineStatuses: debuffs and buffs render as one grid using the
    // PartyDebuffs layout (to the right of the bar). So PartyBuffs only contributes its *filter* (own-only) —
    // its Position/size/growth are ignored in combine mode.
    public static StatusListConfig PartyBuffs()
    {
        return new StatusListConfig
        {
            OwnOnly = true,
            IconSize = 24f,
            PerLine = 12,
            FontSize = 10f
        };
    }

    // --- Enemy list row ---
    public static StatusListConfig EnemyBuffs()
    {
        return new StatusListConfig { Enabled = false };
    }

    public static StatusListConfig EnemyDebuffs()
    {
        // Only debuffs I applied, bottom-right of the 220-wide bar (header 14 + bar 16), growing left.
        return new StatusListConfig
        {
            OwnOnly = true,
            Position = new Vector2(220f - 22f, 14f + 16f + 4f + 2f + 4f),
            GrowRight = false,
            IconSize = 22f,
            FontSize = 10f
        };
    }

    public static StatusListConfig PartyDebuffs()
    {
        // Cleansable debuffs only, to the right of the 240-wide / 20-tall health bar, growing right,
        // vertically centered on it.
        return new StatusListConfig
        {
            CleansableOnly = true,
            Position = new Vector2(244f, 10f - 24f / 2f),
            GrowRight = true,
            IconSize = 24f,
            PerLine = 12,
            FontSize = 10f
        };
    }
}
