namespace SelUI.Modules.CastBar;

/// <summary>
///     The player's cast bar — an independent HUD element (not part of the player unit frame). Only
///     position and size are user-configurable; the look (colors, the spell name above the bar, the
///     square spell icon docked to its left) is baked.
/// </summary>
public sealed class CastBarConfig : ModuleConfig
{
    /// <summary>Top-left screen position of the bar (the spell name sits above it, the icon to its left).</summary>
    public Vector2 Position { get; set; } = new(760f, 680f);

    public float Width { get; set; } = 280f;
    public float BarHeight { get; set; } = 20f;
    public float NameFontSize { get; set; } = 20f;
}
