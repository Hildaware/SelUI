using Newtonsoft.Json;

namespace SelUI.Modules.Alliance;

/// <summary>
///     Settings for the alliance frames: two columns of compact unit frames, one per member of the two
///     *other* alliances (your own party uses the party frames). The row appearance is baked
///     (<see cref="UnitFrames.UnitFrameConfig.AllianceRowDefault" />); only the position is user state.
/// </summary>
public sealed class AllianceFramesConfig : ModuleConfig
{
    /// <summary>Top-left position of the first column's first row.</summary>
    public Vector2 Position { get; set; } = new(40f, 100f);

    /// <summary>
    ///     Preview a full pair of alliances (mock jobs/HP) for positioning. Per-session only: never
    ///     persisted, so it always starts off when the plugin loads.
    /// </summary>
    [JsonIgnore] public bool PreviewMode { get; set; }
}
