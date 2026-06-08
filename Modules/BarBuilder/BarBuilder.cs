using Dalamud.Bindings.ImGui;
using SelUI.Rendering;

namespace SelUI.Modules.BarBuilder;

/// <summary>
///     TEMPORARY visual bar tuner. Draws a single preview bar using a fully parameterized copy of
///     <see cref="BarRenderer" />'s draw logic, with every baked constant exposed as a live slider. Lets
///     the bar look be dialed in visually in-game; the config panel prints the chosen values formatted as
///     the renderer constants for transcription. Drawn at scale 1.0 (ignores RenderScale) so the numbers
///     map 1:1 onto <see cref="BarRenderer" />'s reference-scale constants.
///
///     Delete this folder + its wiring in Plugin.cs and Configuration.cs once the values are chosen.
/// </summary>
public sealed class BarBuilder : IHudModule, IMovableModule
{
    private readonly BarBuilderConfig _config;

    public BarBuilder(BarBuilderConfig config)
    {
        _config = config;
    }

    public string Name => "Bar Builder (temp)";

    public ModuleConfig Config => _config;

    public string EditLabel => "Bar Builder";

    public Vector2 EditTopLeft => _config.Position - new Vector2(Margin);

    public Vector2 EditSize => new Vector2(_config.Width, _config.Height) + new Vector2(Margin * 2f);

    /// <summary>Glow breathing room around the bar so neither glow is clipped by the host window.</summary>
    private float Margin =>
        MathF.Max(_config.BloomReach, _config.BorderGlowReach + _config.BorderGlowThickness) + _config.BorderThickness + 4f;

    public void MoveBy(Vector2 delta) => _config.Position += delta;

    public void Dispose()
    {
    }

    public void Draw()
    {
        var cfg = _config;
        var barPos = cfg.Position;
        var barSize = new Vector2(cfg.Width, cfg.Height);
        var margin = Margin;

        var windowPos = barPos - new Vector2(margin);
        var windowSize = barSize + new Vector2(margin * 2f);

        DrawHelper.DrawInWindow("SelUI_BarBuilder", windowPos, windowSize, false,
            dl => DrawBar(dl, barPos, barSize));
    }

    /// <summary>
    ///     A faithful, parameterized re-implementation of the bar layering, reading every value from
    ///     <see cref="_config" />. Two glow styles can be toggled independently: the original <em>fill</em>
    ///     bloom (radiates from the fill edge) and the new <em>border</em> flare (a soft halo on the whole
    ///     bar outline, to soften the crisp core border). Single fill, fills left→right.
    /// </summary>
    private void DrawBar(ImDrawListPtr dl, Vector2 pos, Vector2 size)
    {
        var cfg = _config;
        var fill = Colors.FromVector4(cfg.FillColor);
        var rounding = cfg.Rounding;

        // 1. Background: the fill color darkened, at reduced opacity.
        var bg = ApplyAlpha(Darken(fill, cfg.BgDarken), cfg.BgOpacity);
        dl.AddRectFilled(pos, pos + size, bg, rounding);

        // 2 + 3. Optional fill bloom, then the gradient fill, over the filled fraction.
        var frac = Math.Clamp(cfg.Fraction, 0f, 1f);
        if (frac > 0f)
        {
            var fillPos = pos;
            var fillEnd = pos + new Vector2(size.X * frac, size.Y);

            if (cfg.EnableFillGlow)
                DrawFillBloom(dl, fillPos, fillEnd, fill);

            // Gradient is anchored to a *full* bar: the right end darkens to DarkenFactor at 100%, and the
            // visible fill samples that gradient at `frac`. So a given pixel keeps its color as the bar
            // fills/unfills — the darken doesn't ride the moving fill front.
            var fullRight = Darken(fill, cfg.DarkenFactor);
            var left = ApplyAlpha(fill, cfg.FillOpacity);
            var right = ApplyAlpha(LerpColor(fill, fullRight, frac), cfg.FillOpacity);
            DrawRoundedGradient(dl, fillPos, fillEnd, left, right);
        }

        // 4. Soft border flare: faint, expanding outline rings in the fill color, drawn under the core
        //    border so the edge reads as a soft halo rather than a hard line.
        if (cfg.EnableBorderGlow)
            DrawBorderGlow(dl, pos, size, fill);

        // 5. Crisp core border: the fill color forced opaque, alpha-scaled by BorderOpacity.
        if (cfg.BorderThickness > 0f && cfg.BorderOpacity > 0f)
        {
            var border = ApplyAlpha(OpaqueColor(fill), cfg.BorderOpacity);
            dl.AddRect(pos, pos + size, border, rounding, ImDrawFlags.None, cfg.BorderThickness);
        }
    }

    /// <summary>The original look: an outward bloom radiating from the fill rectangle's edges.</summary>
    private void DrawFillBloom(ImDrawListPtr dl, Vector2 fillPos, Vector2 fillEnd, uint baseColor)
    {
        var cfg = _config;
        var rounding = cfg.Rounding;
        var passes = Math.Max(1, cfg.BloomPasses);

        // Largest/faintest pass first, tighter/brighter layered on top.
        for (var pass = passes; pass >= 1; pass--)
        {
            var t = (float)pass / passes;
            var expand = cfg.BloomReach * t;
            var alphaFactor = cfg.BloomIntensity * MathF.Exp(-t * cfg.BloomDecay);
            var bloomColor = ApplyAlpha(baseColor, alphaFactor);

            dl.AddRectFilled(
                new Vector2(fillPos.X - expand, fillPos.Y - expand),
                new Vector2(fillEnd.X + expand, fillEnd.Y + expand),
                bloomColor, rounding + expand);
        }
    }

    /// <summary>
    ///     The new look: a soft outward flare on the whole bar outline. Draws several rounded-rect outlines
    ///     in the fill color, each one step further out and fainter, so the border bleeds into a halo
    ///     instead of a crisp 1px line. Outermost/faintest first.
    /// </summary>
    private void DrawBorderGlow(ImDrawListPtr dl, Vector2 pos, Vector2 size, uint baseColor)
    {
        var cfg = _config;
        var rounding = cfg.Rounding;
        var passes = Math.Max(1, cfg.BorderGlowPasses);
        var opaque = OpaqueColor(baseColor);
        var end = pos + size;

        for (var pass = passes; pass >= 1; pass--)
        {
            var t = (float)pass / passes;
            var expand = cfg.BorderGlowReach * t;
            var alphaFactor = cfg.BorderGlowIntensity * MathF.Exp(-t * cfg.BorderGlowDecay);
            var ringColor = ApplyAlpha(opaque, alphaFactor);

            dl.AddRect(
                new Vector2(pos.X - expand, pos.Y - expand),
                new Vector2(end.X + expand, end.Y + expand),
                ringColor, rounding + expand, ImDrawFlags.None, cfg.BorderGlowThickness);
        }
    }

    /// <summary>
    ///     Horizontal gradient fill with <em>both</em> ends rounded to the bar's corner radius (clamped to
    ///     the fill's half-width and half-height). Rounded end-caps in the endpoint colors, with a flat
    ///     gradient strip between them — so a partial bar gets a rounded leading edge, not a square front.
    /// </summary>
    private void DrawRoundedGradient(ImDrawListPtr dl, Vector2 fillPos, Vector2 fillEnd, uint left, uint right)
    {
        var width = fillEnd.X - fillPos.X;
        var height = fillEnd.Y - fillPos.Y;
        if (width <= 0f) return;

        var inset = MathF.Max(0f, MathF.Min(_config.Rounding, MathF.Min(width / 2f, height / 2f)));

        // No rounding: a plain gradient rect.
        if (inset <= 0.01f)
        {
            dl.AddRectFilledMultiColor(fillPos, fillEnd, left, right, right, left);
            return;
        }

        // Too narrow for a middle strip: one rounded rect in the midpoint color.
        if (inset * 2f >= width)
        {
            dl.AddRectFilled(fillPos, fillEnd, LerpColor(left, right, 0.5f), inset, ImDrawFlags.RoundCornersAll);
            return;
        }

        dl.AddRectFilled(fillPos, new Vector2(fillPos.X + inset, fillEnd.Y), left, inset, ImDrawFlags.RoundCornersLeft);
        dl.AddRectFilled(new Vector2(fillEnd.X - inset, fillPos.Y), fillEnd, right, inset, ImDrawFlags.RoundCornersRight);

        var midMin = new Vector2(fillPos.X + inset, fillPos.Y);
        var midMax = new Vector2(fillEnd.X - inset, fillEnd.Y);
        dl.AddRectFilledMultiColor(midMin, midMax, left, right, right, left);
    }

    public bool DrawConfig()
    {
        var cfg = _config;
        var changed = false;

        ImGui.TextDisabled("Drag in Edit HUD Layout mode to reposition. Drawn at scale 1.0 —");
        ImGui.TextDisabled("the numbers below map 1:1 onto BarRenderer's constants.");
        ImGui.Separator();

        if (ImGui.CollapsingHeader("Geometry", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Slider("Width (px)", () => cfg.Width, v => cfg.Width = v, 20f, 600f, "%.0f");
            changed |= Slider("Height (px)", () => cfg.Height, v => cfg.Height = v, 4f, 80f, "%.0f");
            changed |= Slider("Fill fraction", () => cfg.Fraction, v => cfg.Fraction = v, 0f, 1f, "%.2f");
            changed |= Slider("Rounding", () => cfg.Rounding, v => cfg.Rounding = v, 0f, 40f, "%.1f");
        }

        if (ImGui.CollapsingHeader("Fill gradient", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Slider("Fill opacity", () => cfg.FillOpacity, v => cfg.FillOpacity = v, 0f, 1f, "%.2f");
            changed |= Slider("Darken factor (right end)", () => cfg.DarkenFactor, v => cfg.DarkenFactor = v, 0f, 1f, "%.2f");
        }

        if (ImGui.CollapsingHeader("Background", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Slider("Bg darken (brightness)", () => cfg.BgDarken, v => cfg.BgDarken = v, 0f, 1f, "%.2f");
            changed |= Slider("Bg opacity", () => cfg.BgOpacity, v => cfg.BgOpacity = v, 0f, 1f, "%.2f");
        }

        if (ImGui.CollapsingHeader("Fill glow (radiates from fill edge)", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var fillGlow = cfg.EnableFillGlow;
            if (ImGui.Checkbox("Enable fill glow", ref fillGlow))
            {
                cfg.EnableFillGlow = fillGlow;
                changed = true;
            }

            var passes = cfg.BloomPasses;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.SliderInt("Bloom passes", ref passes, 1, 24))
            {
                cfg.BloomPasses = passes;
                changed = true;
            }

            changed |= Slider("Bloom reach (px)", () => cfg.BloomReach, v => cfg.BloomReach = v, 0f, 20f, "%.2f");
            changed |= Slider("Bloom intensity", () => cfg.BloomIntensity, v => cfg.BloomIntensity = v, 0f, 1f, "%.2f");
            changed |= Slider("Bloom decay (falloff)", () => cfg.BloomDecay, v => cfg.BloomDecay = v, 0f, 8f, "%.2f");
        }

        if (ImGui.CollapsingHeader("Border", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Slider("Core thickness", () => cfg.BorderThickness, v => cfg.BorderThickness = v, 0f, 6f, "%.2f");
            changed |= Slider("Core opacity", () => cfg.BorderOpacity, v => cfg.BorderOpacity = v, 0f, 1f, "%.2f");
            ImGui.TextDisabled("(Drop core opacity and lean on the border glow for a soft edge.)");
        }

        if (ImGui.CollapsingHeader("Border glow (soft outline flare)", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var borderGlow = cfg.EnableBorderGlow;
            if (ImGui.Checkbox("Enable border glow", ref borderGlow))
            {
                cfg.EnableBorderGlow = borderGlow;
                changed = true;
            }

            var passes = cfg.BorderGlowPasses;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.SliderInt("Glow passes", ref passes, 1, 24))
            {
                cfg.BorderGlowPasses = passes;
                changed = true;
            }

            changed |= Slider("Glow reach (px)", () => cfg.BorderGlowReach, v => cfg.BorderGlowReach = v, 0f, 20f, "%.2f");
            changed |= Slider("Glow intensity", () => cfg.BorderGlowIntensity, v => cfg.BorderGlowIntensity = v, 0f, 1f, "%.2f");
            changed |= Slider("Glow decay (falloff)", () => cfg.BorderGlowDecay, v => cfg.BorderGlowDecay = v, 0f, 8f, "%.2f");
            changed |= Slider("Ring thickness", () => cfg.BorderGlowThickness, v => cfg.BorderGlowThickness = v, 0.5f, 8f, "%.2f");
        }

        if (ImGui.CollapsingHeader("Preview color", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var col = cfg.FillColor;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.ColorEdit4("Fill color", ref col))
            {
                cfg.FillColor = col;
                changed = true;
            }

            ImGui.TextDisabled("(Sample tint only — not a renderer constant.)");
        }

        ImGui.Separator();
        DrawReadout();

        return changed;
    }

    /// <summary>Prints the current values as the BarRenderer constants, with a button to copy them.</summary>
    private void DrawReadout()
    {
        var cfg = _config;
        var text =
            $"// BarRenderer constants\n" +
            $"Rounding          = {cfg.Rounding:0.###}f;\n" +
            $"FillOpacity       = {cfg.FillOpacity:0.###}f;\n" +
            $"DarkenFactor      = {cfg.DarkenFactor:0.###}f;\n" +
            $"BgDarken          = {cfg.BgDarken:0.###}f;\n" +
            $"BgOpacity         = {cfg.BgOpacity:0.###}f;\n" +
            $"BorderThickness   = {cfg.BorderThickness:0.###}f;   // crisp core line\n" +
            $"BorderOpacity     = {cfg.BorderOpacity:0.###}f;\n" +
            $"// fill glow: {(cfg.EnableFillGlow ? "ON" : "OFF")}\n" +
            $"BloomPasses       = {cfg.BloomPasses};\n" +
            $"BloomReach        = {cfg.BloomReach:0.###}f;\n" +
            $"BloomIntensity    = {cfg.BloomIntensity:0.###}f;\n" +
            $"BloomDecay        = {cfg.BloomDecay:0.###}f;   // exp(-t * this)\n" +
            $"// border glow: {(cfg.EnableBorderGlow ? "ON" : "OFF")}\n" +
            $"BorderGlowPasses    = {cfg.BorderGlowPasses};\n" +
            $"BorderGlowReach     = {cfg.BorderGlowReach:0.###}f;\n" +
            $"BorderGlowIntensity = {cfg.BorderGlowIntensity:0.###}f;\n" +
            $"BorderGlowDecay     = {cfg.BorderGlowDecay:0.###}f;\n" +
            $"BorderGlowThickness = {cfg.BorderGlowThickness:0.###}f;\n" +
            $"// geometry: {cfg.Width:0}x{cfg.Height:0}, fill {cfg.Fraction:0.##}";

        if (ImGui.Button("Copy values to clipboard"))
            ImGui.SetClipboardText(text);

        ImGui.TextUnformatted(text);
    }

    private static bool Slider(string label, Func<float> get, Action<float> set, float min, float max, string fmt)
    {
        var v = get();
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat(label, ref v, min, max, fmt))
        {
            set(Math.Clamp(v, min, max));
            return true;
        }

        return false;
    }

    // --- Local copies of BarRenderer's private color helpers (kept identical for a faithful preview). ---

    private static uint ApplyAlpha(uint color, float factor)
    {
        var alpha = (uint)MathF.Round(((color >> 24) & 0xFF) * Math.Clamp(factor, 0f, 1f));
        return (color & 0x00FFFFFF) | (Math.Min(alpha, 255u) << 24);
    }

    private static uint OpaqueColor(uint color) => color | 0xFF000000;

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
            // Channels must be lerped in float space: as uint, cb - ca underflows when cb < ca (i.e. when
            // darkening), which is what made any DarkenFactor < 1 collapse to black.
            float ca = (a >> shift) & 0xFF;
            float cb = (b >> shift) & 0xFF;
            return (uint)MathF.Round(ca + (cb - ca) * t) & 0xFF;
        }
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
}
