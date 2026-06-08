using SelUI.Rendering;

namespace SelUI.Modules.BarBuilder;

/// <summary>
///     TEMPORARY tuning module config. Every value that <see cref="BarRenderer" /> bakes as a constant
///     is exposed here as live, persisted state so the look can be dialed in visually in-game and the
///     resulting numbers transcribed back into the renderer constants. Defaults mirror the current
///     baked constants exactly, so an untouched builder shows today's bar. Delete this whole folder (and
///     its wiring in Plugin/Configuration) once the values are chosen.
/// </summary>
public sealed class BarBuilderConfig : ModuleConfig
{
    public BarBuilderConfig()
    {
        // Off by default — this is a dev tool, not part of the HUD.
        Enabled = false;
    }

    /// <summary>Where the preview bar is anchored (top-left of the bar itself). Draggable in edit mode.</summary>
    public Vector2 Position { get; set; } = new(600f, 400f);

    // --- Geometry (reference pixels; the preview draws at scale 1.0 so these map 1:1 to constants) ---
    public float Width { get; set; } = 200f;
    public float Height { get; set; } = 20f;
    public float Fraction { get; set; } = 0.7f;
    public float Rounding { get; set; } = 12f; // BarRenderer.Rounding

    // --- Fill gradient ---
    public float FillOpacity { get; set; } = 0.4f;   // BarRenderer.FillOpacity
    public float DarkenFactor { get; set; } = 0.55f; // BarRenderer.DarkenFactor

    // --- Background (derived from the fill color) ---
    public float BgDarken { get; set; } = 0.15f; // BarRenderer.BgDarken
    public float BgOpacity { get; set; } = 0.5f;  // BarRenderer.BgOpacity

    // --- Fill glow / bloom (the current look: bloom radiating from the fill edge) ---
    public bool EnableFillGlow { get; set; } = false; // off by default now — trying the border-flare look
    public int BloomPasses { get; set; } = 8;       // BarRenderer.BloomPasses
    public float BloomReach { get; set; } = 3f;     // BarRenderer.BloomReach
    public float BloomIntensity { get; set; } = 0.4f; // BarRenderer.BloomIntensity
    public float BloomDecay { get; set; } = 2.5f;   // hardcoded exp(-t * 2.5) in DrawGlowFill

    // --- Border ---
    public float BorderThickness { get; set; } = 1f; // crisp core line; default borderThickness arg
    public float BorderOpacity { get; set; } = 1f;   // border is the opaque fill color; this scales its alpha

    // --- Border glow (the new look: a soft outward flare on the whole bar outline) ---
    public bool EnableBorderGlow { get; set; } = true;
    public int BorderGlowPasses { get; set; } = 6;
    public float BorderGlowReach { get; set; } = 4f;     // how far the halo bleeds outward, px
    public float BorderGlowIntensity { get; set; } = 0.5f;
    public float BorderGlowDecay { get; set; } = 2f;      // exp(-t * this) falloff
    public float BorderGlowThickness { get; set; } = 2f;  // thickness of each halo ring

    // --- Preview fill color (not a renderer constant; just what we tint the sample bar) ---
    public Vector4 FillColor { get; set; } = Colors.ToVector4(Colors.Hp);
}
