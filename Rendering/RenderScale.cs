namespace SelUI.Rendering;

/// <summary>
///     The single source of truth for SelUI's global render scale: a user-controlled multiplier on
///     every baked pixel dimension and font size, so the whole HUD grows or shrinks uniformly. 1.0 is
///     the baked look; the user nudges it with the "Overall Scale" slider, persisted as
///     <c>Configuration.UiScale</c> and pushed into <see cref="Value" /> on load and on every change.
/// </summary>
public sealed class RenderScale
{
    /// <summary>Multiply every baked pixel dimension by this. 1.0 = the baked sizes.</summary>
    public float Value { get; set; } = 1f;
}
