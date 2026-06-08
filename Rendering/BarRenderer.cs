using Dalamud.Bindings.ImGui;

namespace SelUI.Rendering;

/// <summary>A single fill layer of a bar: a fraction (0..1) of the bar's length, in a given color.</summary>
public readonly record struct BarFill(float Fraction, uint Color);

/// <summary>
///     Draws SelUI's bars. The signature look, with no knobs: a rounded, fill-tinted background; a
///     translucent fill that gradients from the bar color to a darker shade — anchored to a *full* bar so
///     the darkening stays put as the bar drains — with a rounded leading edge; a soft outward flare on
///     the bar outline; and a crisp border in the bar's own color over the top.
/// </summary>
public sealed class BarRenderer
{
    /// <summary>Corner radius for every bar, at the reference UI scale (scaled per-frame by <see cref="RenderScale" />).</summary>
    public const float Rounding = 12f;

    private const float FillOpacity = 0.7f;   // translucent fill
    private const float DarkenFactor = 0.3f;  // right end of the full-bar gradient is this fraction of the bar color
    private const float BgDarken = 0.2f;      // background = the bar color darkened to this brightness
    private const float BgOpacity = 0.65f;    // background opacity

    // Soft outward flare on the bar outline, in the bar's own color, drawn under the crisp border so the
    // edge reads as a glow rather than a hard line. All at the reference UI scale.
    private const int BorderGlowPasses = 6;
    private const float BorderGlowReach = 4f;       // how far the halo bleeds outward, px
    private const float BorderGlowIntensity = 0.4f;
    private const float BorderGlowDecay = 2f;       // exp(-t * this) falloff
    private const float BorderGlowThickness = 2f;   // thickness of each halo ring

    private readonly RenderScale _scale;

    public BarRenderer(RenderScale scale)
    {
        _scale = scale;
    }

    /// <summary>Corner radius scaled to the current UI scale.</summary>
    private float ScaledRounding => Rounding * _scale.Value;

    public void Draw(
        ImDrawListPtr drawList,
        Vector2 pos,
        Vector2 size,
        uint backgroundColor,
        ReadOnlySpan<BarFill> fills,
        uint borderColor = 0,
        float borderThickness = 1f,
        BarDirection direction = BarDirection.Right,
        float alpha = 1f,
        uint borderOverride = 0)
    {
        // Background is a very dark version of the bar's own color for strong contrast (falls back to
        // the passed color only when there's no fill to derive from).
        var bg = fills.Length > 0 ? ApplyAlpha(Darken(fills[0].Color, BgDarken), BgOpacity) : backgroundColor;
        var rounding = ScaledRounding;
        drawList.AddRectFilled(pos, pos + size, ApplyAlpha(bg, alpha), rounding);

        foreach (var fill in fills)
        {
            var frac = Math.Clamp(fill.Fraction, 0f, 1f);
            if (frac <= 0f) continue;

            var (fillPos, fillSize) = FillRect(pos, size, frac, direction);
            var fillEnd = fillPos + fillSize;
            var baseColor = ApplyAlpha(fill.Color, alpha);

            // Gradient anchored to a *full* bar: the right end darkens to DarkenFactor at 100%, and the
            // visible fill samples that gradient at `frac`. So a given pixel keeps its color as the bar
            // fills/unfills — the darken doesn't ride the moving fill front.
            var fullRight = Darken(baseColor, DarkenFactor);
            var left = ApplyAlpha(baseColor, FillOpacity);
            var right = ApplyAlpha(LerpColor(baseColor, fullRight, frac), FillOpacity);

            // The fill starts at the bar's edge (round that cap) and its leading edge is always rounded.
            var leftAligned = MathF.Abs(fillPos.X - pos.X) < 0.5f;
            DrawGradientFill(drawList, fillPos, fillEnd, left, right, leftAligned, true);
        }

        // Soft border flare, in the bar's own color, under the crisp border.
        var glowBase = fills.Length > 0 ? OpaqueColor(fills[0].Color) : borderColor;
        if (glowBase != 0)
            DrawBorderGlow(drawList, pos, size, ApplyAlpha(glowBase, alpha));

        // Border: explicit override (e.g. a selection highlight) wins, otherwise it matches the bar's
        // own color. Drawn crisp over the translucent fill.
        var border = borderOverride != 0 ? borderOverride : fills.Length > 0 ? OpaqueColor(fills[0].Color) : borderColor;
        if (border != 0 && borderThickness > 0f)
            drawList.AddRect(pos, pos + size, ApplyAlpha(border, alpha), rounding, ImDrawFlags.None, borderThickness * _scale.Value);
    }

    /// <summary>Convenience overload for the common single-fill bar.</summary>
    public void Draw(
        ImDrawListPtr drawList,
        Vector2 pos,
        Vector2 size,
        uint backgroundColor,
        float fraction,
        uint fillColor,
        uint borderColor = 0,
        float borderThickness = 1f,
        BarDirection direction = BarDirection.Right,
        float alpha = 1f,
        uint borderOverride = 0)
    {
        Span<BarFill> fills = [new BarFill(fraction, fillColor)];
        Draw(drawList, pos, size, backgroundColor, fills, borderColor, borderThickness, direction, alpha, borderOverride);
    }

    /// <summary>
    ///     Draw a gradient fill segment spanning <paramref name="startFrac" />..<paramref name="endFrac" />
    ///     of the bar, in the bar's signature gradient look. Used to overlay an absorb shield on a health
    ///     bar (the shield starts at the HP fill and runs rightward). Like the primary fill, the gradient
    ///     is anchored to the full bar. Only ends that line up with the bar's bounds get rounded, so an
    ///     interior span (the shield's left edge, butting the HP fill) keeps a square end.
    /// </summary>
    public void DrawSegment(ImDrawListPtr drawList, Vector2 pos, Vector2 size, uint color, float startFrac, float endFrac, float alpha = 1f)
    {
        startFrac = Math.Clamp(startFrac, 0f, 1f);
        endFrac = Math.Clamp(endFrac, 0f, 1f);
        if (endFrac - startFrac <= 0.0005f) return;

        var fillPos = new Vector2(pos.X + size.X * startFrac, pos.Y);
        var fillEnd = new Vector2(pos.X + size.X * endFrac, pos.Y + size.Y);
        var baseColor = ApplyAlpha(color, alpha);

        var fullDark = Darken(baseColor, DarkenFactor);
        var left = ApplyAlpha(LerpColor(baseColor, fullDark, startFrac), FillOpacity);
        var right = ApplyAlpha(LerpColor(baseColor, fullDark, endFrac), FillOpacity);

        var leftAligned = MathF.Abs(fillPos.X - pos.X) < 0.5f;
        var rightAligned = MathF.Abs(fillEnd.X - (pos.X + size.X)) < 0.5f;
        DrawGradientFill(drawList, fillPos, fillEnd, left, right, leftAligned, rightAligned);
    }

    /// <summary>
    ///     A soft outward flare on the whole bar outline: several rounded-rect outlines in the bar color,
    ///     each stepped further out and fainter, so the border bleeds into a halo. Outermost/faintest first.
    /// </summary>
    private void DrawBorderGlow(ImDrawListPtr drawList, Vector2 pos, Vector2 size, uint baseColor)
    {
        var rounding = ScaledRounding;
        var reach = BorderGlowReach * _scale.Value;
        var thickness = BorderGlowThickness * _scale.Value;
        var end = pos + size;

        for (var pass = BorderGlowPasses; pass >= 1; pass--)
        {
            var t = (float)pass / BorderGlowPasses;
            var expand = reach * t;
            var alphaFactor = BorderGlowIntensity * MathF.Exp(-t * BorderGlowDecay);
            var ringColor = ApplyAlpha(baseColor, alphaFactor);

            drawList.AddRect(
                new Vector2(pos.X - expand, pos.Y - expand),
                new Vector2(end.X + expand, end.Y + expand),
                ringColor, rounding + expand, ImDrawFlags.None, thickness);
        }
    }

    /// <summary>
    ///     Horizontal gradient fill with rounded end-caps on the requested ends (clamped to the fill's
    ///     half-width and half-height): solid rounded caps in the endpoint colors, a flat gradient strip
    ///     between them.
    /// </summary>
    private void DrawGradientFill(ImDrawListPtr drawList, Vector2 fillPos, Vector2 fillEnd, uint left, uint right, bool roundLeft, bool roundRight)
    {
        var width = fillEnd.X - fillPos.X;
        var height = fillEnd.Y - fillPos.Y;
        if (width <= 0f) return;

        var rounding = ScaledRounding;
        var maxR = MathF.Max(0f, MathF.Min(rounding, MathF.Min(width / 2f, height / 2f)));
        var leftInset = roundLeft ? maxR : 0f;
        var rightInset = roundRight ? maxR : 0f;

        // Too narrow for a middle strip: a single rounded rect in the midpoint color.
        if (leftInset + rightInset >= width)
        {
            var flags = ImDrawFlags.RoundCornersNone;
            if (roundLeft) flags |= ImDrawFlags.RoundCornersLeft;
            if (roundRight) flags |= ImDrawFlags.RoundCornersRight;
            drawList.AddRectFilled(fillPos, fillEnd, LerpColor(left, right, 0.5f), rounding, flags);
            return;
        }

        if (leftInset > 0f)
            drawList.AddRectFilled(fillPos, new Vector2(fillPos.X + leftInset, fillEnd.Y), left, rounding,
                ImDrawFlags.RoundCornersLeft);

        if (rightInset > 0f)
            drawList.AddRectFilled(new Vector2(fillEnd.X - rightInset, fillPos.Y), fillEnd, right, rounding,
                ImDrawFlags.RoundCornersRight);

        var midMin = new Vector2(fillPos.X + leftInset, fillPos.Y);
        var midMax = new Vector2(fillEnd.X - rightInset, fillEnd.Y);
        drawList.AddRectFilledMultiColor(midMin, midMax, left, right, right, left);
    }

    private static uint ApplyAlpha(uint color, float factor)
    {
        var alpha = (uint)MathF.Round(((color >> 24) & 0xFF) * Math.Clamp(factor, 0f, 1f));
        return (color & 0x00FFFFFF) | (Math.Min(alpha, 255u) << 24);
    }

    private static uint OpaqueColor(uint color)
    {
        return color | 0xFF000000;
    }

    private static uint Darken(uint color, float factor)
    {
        var f = Math.Clamp(factor, 0f, 1f);
        var r = Math.Min((uint)MathF.Round((color & 0xFF) * f), 255u);
        var g = Math.Min((uint)MathF.Round(((color >> 8) & 0xFF) * f), 255u);
        var b = Math.Min((uint)MathF.Round(((color >> 16) & 0xFF) * f), 255u);
        var a = (color >> 24) & 0xFF;
        return (a << 24) | (b << 16) | (g << 8) | r;
    }

    /// <summary>Componentwise lerp of two packed ABGR colors (alpha included).</summary>
    private static uint LerpColor(uint a, uint b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var r = Lerp8(a, b, t, 0);
        var g = Lerp8(a, b, t, 8);
        var bch = Lerp8(a, b, t, 16);
        var al = Lerp8(a, b, t, 24);
        return (al << 24) | (bch << 16) | (g << 8) | r;

        static uint Lerp8(uint a, uint b, float t, int shift)
        {
            // Lerp in float space: as uint, cb - ca underflows when cb < ca (i.e. when darkening).
            float ca = (a >> shift) & 0xFF;
            float cb = (b >> shift) & 0xFF;
            return (uint)MathF.Round(ca + (cb - ca) * t) & 0xFF;
        }
    }

    private static (Vector2 pos, Vector2 size) FillRect(Vector2 pos, Vector2 size, float frac, BarDirection direction)
    {
        return direction switch
        {
            BarDirection.Right => (pos, new Vector2(size.X * frac, size.Y)),
            BarDirection.Left => (pos + new Vector2(size.X * (1f - frac), 0f), new Vector2(size.X * frac, size.Y)),
            BarDirection.Up => (pos + new Vector2(0f, size.Y * (1f - frac)), new Vector2(size.X, size.Y * frac)),
            BarDirection.Down => (pos, new Vector2(size.X, size.Y * frac)),
            _ => (pos, new Vector2(size.X * frac, size.Y))
        };
    }
}
