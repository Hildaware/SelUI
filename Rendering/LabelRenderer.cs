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

    public LabelRenderer(FontManager fonts)
    {
        _fonts = fonts;
    }

    /// <summary>
    ///     The font used when a draw call does not specify one. Set this to the user's global font choice;
    ///     null falls back to the bundled default.
    /// </summary>
    public SingleFontSpec? GlobalFont { get; set; }

    /// <summary>Measure <paramref name="text" /> at <paramref name="fontSize" /> px using the given (or global) font.</summary>
    public Vector2 Measure(string text, float fontSize, SingleFontSpec? font = null)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        var handle = _fonts.GetHandle(font ?? GlobalFont);
        if (!handle.Available) return Vector2.Zero;

        using (handle.Push())
        {
            var scale = fontSize / FontManager.AtlasBakedSize;
            return ImGui.CalcTextSize(text) * scale;
        }
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
            var scale = fontSize / FontManager.AtlasBakedSize;
            var textSize = ImGui.CalcTextSize(text) * scale;
            var pos = DrawHelper.GetAnchoredPosition(anchorPos, textSize, anchor);

            // Drop shadow, offset 2px toward the bottom-right.
            if (shadow)
                drawList.AddText(imFont, fontSize, pos + new Vector2(2f, 2f), shadowColor, text);

            if (outline)
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    drawList.AddText(imFont, fontSize, pos + new Vector2(x, y), outlineColor, text);
                }

            drawList.AddText(imFont, fontSize, pos, color, text);
        }
    }
}
