using Dalamud.Bindings.ImGui;

namespace SelUI.Rendering;

/// <summary>A single fill layer of a bar: a fraction (0..1) of the bar's length, in a given color.</summary>
public readonly record struct BarFill(float Fraction, uint Color);

/// <summary>
///     Draws SelUI's bars. The signature look, with no knobs: a rounded background, a translucent fill
///     that gradients from the bar color on the left to a darker shade on the right, a tight outward
///     glow, and a crisp border in the bar's own color.
/// </summary>
public sealed class BarRenderer
{
    /// <summary>Corner radius for every bar.</summary>
    public const float Rounding = 12f;

    private const int BloomPasses = 8;
    private const float FillOpacity = 0.4f;   // translucent fill
    private const float BloomReach = 3f;      // glow spread in px (tight)
    private const float BloomIntensity = 0.4f;
    private const float DarkenFactor = 0.55f; // right end of the gradient is this fraction of the bar color
    private const float BgDarken = 0.15f;     // background = the bar color darkened to this brightness
    private const float BgOpacity = 0.5f;     // background opacity

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
        drawList.AddRectFilled(pos, pos + size, ApplyAlpha(bg, alpha), Rounding);

        foreach (var fill in fills)
        {
            var frac = Math.Clamp(fill.Fraction, 0f, 1f);
            if (frac <= 0f) continue;

            var (fillPos, fillSize) = FillRect(pos, size, frac, direction);
            DrawGlowFill(drawList, fillPos, fillPos + fillSize, ApplyAlpha(fill.Color, alpha), pos, size);
        }

        // Border: explicit override (e.g. a selection highlight) wins, otherwise it matches the bar's
        // own color. Drawn crisp over the translucent fill.
        var border = borderOverride != 0 ? borderOverride : fills.Length > 0 ? OpaqueColor(fills[0].Color) : borderColor;
        if (border != 0 && borderThickness > 0f)
            drawList.AddRect(pos, pos + size, ApplyAlpha(border, alpha), Rounding, ImDrawFlags.None, borderThickness);
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
    ///     Draw a glowing fill segment spanning <paramref name="startFrac" />..<paramref name="endFrac" />
    ///     of the bar, in the bar's signature glow/gradient look. Used to overlay an absorb shield on a
    ///     health bar (the shield starts at the HP fill and runs rightward). Corners that line up with the
    ///     bar's own bounds get rounded; an interior span keeps square ends.
    /// </summary>
    public void DrawSegment(ImDrawListPtr drawList, Vector2 pos, Vector2 size, uint color, float startFrac, float endFrac, float alpha = 1f)
    {
        startFrac = Math.Clamp(startFrac, 0f, 1f);
        endFrac = Math.Clamp(endFrac, 0f, 1f);
        if (endFrac - startFrac <= 0.0005f) return;

        var fillPos = new Vector2(pos.X + size.X * startFrac, pos.Y);
        var fillEnd = new Vector2(pos.X + size.X * endFrac, pos.Y + size.Y);
        DrawGlowFill(drawList, fillPos, fillEnd, ApplyAlpha(color, alpha), pos, size);
    }

    private static void DrawGlowFill(ImDrawListPtr drawList, Vector2 fillPos, Vector2 fillEnd, uint baseColor, Vector2 bgPos, Vector2 bgSize)
    {
        // Tight outward glow: bloom passes largest/faintest first, tighter/brighter ones layered on top.
        for (var pass = BloomPasses; pass >= 1; pass--)
        {
            var t = (float)pass / BloomPasses;
            var expand = BloomReach * t;
            var alphaFactor = BloomIntensity * MathF.Exp(-t * 2.5f);
            var bloomColor = ApplyAlpha(baseColor, alphaFactor);

            drawList.AddRectFilled(
                new Vector2(fillPos.X - expand, fillPos.Y - expand),
                new Vector2(fillEnd.X + expand, fillEnd.Y + expand),
                bloomColor, Rounding + expand);
        }

        // Translucent left-to-right gradient: bar color -> darker shade.
        var left = ApplyAlpha(baseColor, FillOpacity);
        var right = ApplyAlpha(Darken(baseColor, DarkenFactor), FillOpacity);
        DrawRoundedGradient(drawList, fillPos, fillEnd, left, right, bgPos, bgSize);
    }

    /// <summary>
    ///     Horizontal gradient that keeps rounded corners: solid rounded end-caps in the gradient's
    ///     endpoint colors, with a flat gradient strip between them. Only the edges of the fill that
    ///     line up with the bar's bounds get rounded (so a partial bar keeps a square fill front).
    /// </summary>
    private static void DrawRoundedGradient(ImDrawListPtr drawList, Vector2 fillPos, Vector2 fillEnd, uint left, uint right, Vector2 bgPos, Vector2 bgSize)
    {
        const float eps = 0.5f;
        var bgEnd = bgPos + bgSize;

        var leftAligned = MathF.Abs(fillPos.X - bgPos.X) < eps;
        var rightAligned = MathF.Abs(fillEnd.X - bgEnd.X) < eps;

        var width = fillEnd.X - fillPos.X;
        var halfW = width / 2f;
        var leftInset = leftAligned ? MathF.Min(Rounding, halfW) : 0f;
        var rightInset = rightAligned ? MathF.Min(Rounding, halfW) : 0f;

        // Too narrow for a middle strip: just a single rounded rect in the brighter color.
        if (leftInset + rightInset >= width)
        {
            var flags = ImDrawFlags.RoundCornersNone;
            if (leftAligned) flags |= ImDrawFlags.RoundCornersLeft;
            if (rightAligned) flags |= ImDrawFlags.RoundCornersRight;
            drawList.AddRectFilled(fillPos, fillEnd, left, Rounding, flags);
            return;
        }

        if (leftInset > 0f)
            drawList.AddRectFilled(fillPos, new Vector2(fillPos.X + leftInset, fillEnd.Y), left, Rounding,
                ImDrawFlags.RoundCornersLeft);

        if (rightInset > 0f)
            drawList.AddRectFilled(new Vector2(fillEnd.X - rightInset, fillPos.Y), fillEnd, right, Rounding,
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
