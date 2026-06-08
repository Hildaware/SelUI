using Dalamud.Bindings.ImGui;
using Dalamud.Interface.FontIdentifier;

namespace SelUI.Rendering;

/// <summary>
///     Draws text with SelUI's managed fonts. Handles anchor alignment and an optional 1px outline
///     so text stays readable over bars and the game world.
/// </summary>
public sealed class LabelRenderer
{
    private readonly FontManager _fonts;
    private readonly RenderScale _scale;

    public LabelRenderer(FontManager fonts, RenderScale scale)
    {
        _fonts = fonts;
        _scale = scale;
    }

    /// <summary>
    ///     The font used when a draw call does not specify one. Set this to the user's global font choice;
    ///     null falls back to the bundled default.
    /// </summary>
    public SingleFontSpec? GlobalFont { get; set; }

    /// <summary>
    ///     The user's personal font-scale fine-tune (the "Font Scale" knob). 1.0 = the baked sizes. This
    ///     multiplies on top of the global Overall Scale, so text scales with the rest of the HUD and
    ///     this just nudges it. Layout code that sizes a box from a raw font-size value should run it
    ///     through <see cref="Scale" /> so the box grows with the text.
    /// </summary>
    public float GlobalScale { get; set; } = 1f;

    /// <summary>The effective font multiplier: the Font Scale fine-tune times the global Overall Scale.</summary>
    private float Total => GlobalScale * _scale.Value;

    /// <summary>
    ///     The rounded-to-whole-pixel size a raw font size renders at. Fonts look bad at fractional sizes
    ///     (e.g. 21.4px), so every measure/draw rounds to the nearest pixel; clamped to ≥1 so text never
    ///     vanishes. Measure and draw share this so layout boxes match the rendered glyphs exactly.
    /// </summary>
    private float EffectiveSize(float fontSize) => MathF.Max(1f, MathF.Round(fontSize * Total));

    /// <summary>The whole-pixel size a raw font-size value used as a layout dimension renders at.</summary>
    public float Scale(float value) => EffectiveSize(value);

    /// <summary>Measure <paramref name="text" /> at <paramref name="fontSize" /> px using the given (or global) font.</summary>
    public Vector2 Measure(string text, float fontSize, SingleFontSpec? font = null)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        var handle = _fonts.GetHandle(font ?? GlobalFont);
        if (!handle.Available) return Vector2.Zero;

        using (handle.Push())
            return MeasurePushed(text, EffectiveSize(fontSize));
    }

    /// <summary>
    ///     The on-screen size of <paramref name="text" /> when drawn at <paramref name="drawSize" /> px with
    ///     the currently-pushed font. We must divide <see cref="ImGui.CalcTextSize" /> by the live render
    ///     size (<see cref="ImGui.GetFontSize" />), NOT the atlas's baked 48px: <c>CalcTextSize</c> bakes in
    ///     Dalamud's <c>io.FontGlobalScale</c> (which varies with resolution / DPI / the global font-scale
    ///     setting), but <c>ImDrawList.AddText(font, drawSize, …)</c> ignores it. Dividing by the live size
    ///     cancels that factor, so the measurement matches the glyphs we actually draw at any scale.
    /// </summary>
    private static Vector2 MeasurePushed(string text, float drawSize)
    {
        var renderSize = ImGui.GetFontSize();
        var scale = renderSize > 0f ? drawSize / renderSize : 0f;
        return ImGui.CalcTextSize(text) * scale;
    }

    /// <summary>
    ///     Draw <paramref name="text" /> so that <paramref name="anchor" /> aligns to <paramref name="anchorPos" />.
    /// </summary>
    public void Draw(
        ImDrawListPtr drawList,
        string text,
        Vector2 anchorPos,
        float fontSize,
        uint color,
        DrawAnchor anchor = DrawAnchor.Center,
        bool outline = true,
        uint outlineColor = 0xFF000000,
        SingleFontSpec? font = null,
        float alpha = 1f,
        bool shadow = true,
        uint shadowColor = 0xB0000000)
    {
        if (string.IsNullOrEmpty(text)) return;

        var handle = _fonts.GetHandle(font ?? GlobalFont);
        if (!handle.Available) return;

        color = Colors.MultiplyAlpha(color, alpha);
        outlineColor = Colors.MultiplyAlpha(outlineColor, alpha);
        shadowColor = Colors.MultiplyAlpha(shadowColor, alpha);

        using (handle.Push())
        {
            var imFont = ImGui.GetFont();
            var drawSize = EffectiveSize(fontSize);
            var textSize = MeasurePushed(text, drawSize);
            var pos = DrawHelper.GetAnchoredPosition(anchorPos, textSize, anchor);

            // Drop shadow, offset 2px (scaled with the UI) toward the bottom-right.
            if (shadow)
                drawList.AddText(imFont, drawSize, pos + new Vector2(2f, 2f) * _scale.Value, shadowColor, text);

            if (outline)
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    drawList.AddText(imFont, drawSize, pos + new Vector2(x, y), outlineColor, text);
                }

            drawList.AddText(imFont, drawSize, pos, color, text);
        }
    }
}
