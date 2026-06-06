using Newtonsoft.Json;
using SelUI.Modules.UnitFrames;

namespace SelUI.Modules.Party;

/// <summary>Settings for the party frames: a vertical stack of unit frames, one per party member.</summary>
public sealed class PartyFramesConfig : ModuleConfig
{
    /// <summary>Top-left position of the first row.</summary>
    public Vector2 Position { get; set; } = new(40f, 320f);

    /// <summary>Vertical pitch between rows (spacing between party frames).</summary>
    public float RowHeight { get; set; } = 64f;

    /// <summary>Stack rows upward from <see cref="Position" /> instead of downward.</summary>
    public bool GrowUp { get; set; }

    /// <summary>Show the party frame (just yourself) even when not in a party.</summary>
    public bool ShowWhenSolo { get; set; }

    /// <summary>
    ///     Preview a full party (using your own character as a stand-in) for positioning/styling. Per-session
    ///     only: never persisted, so it always starts off when the plugin loads.
    /// </summary>
    [JsonIgnore] public bool PreviewMode { get; set; }

    /// <summary>Per-member frame appearance (position is driven by the row layout, not this).</summary>
    public UnitFrameConfig Row { get; set; } = UnitFrameConfig.PartyRowDefault();
}
