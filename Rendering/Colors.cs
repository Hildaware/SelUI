namespace SelUI.Rendering;

/// <summary>
///     Color helpers. SelUI stores colors as packed <see cref="uint" /> values in the ImGui
///     <c>IM_COL32</c> layout (0xAABBGGRR) so they can be handed straight to draw-list calls.
/// </summary>
public static class Colors
{
    /// <summary>Pack a hex RGB string (e.g. "FF5A5A") plus an alpha byte into an ImGui color.</summary>
    public static uint FromHex(string hex, byte alpha = 0xFF)
    {
        var rgb = Convert.ToUInt32(hex, 16);
        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        return ((uint)alpha << 24) | (b << 16) | (g << 8) | r;
    }

    /// <summary>Pack RGBA bytes into an ImGui color.</summary>
    public static uint Rgba(byte r, byte g, byte b, byte a = 0xFF)
    {
        return ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
    }

    /// <summary>Convert a packed ImGui color to the Vector4 layout ImGui's color pickers expect.</summary>
    public static Vector4 ToVector4(uint color)
    {
        var r = (color & 0xFF) / 255f;
        var g = ((color >> 8) & 0xFF) / 255f;
        var b = ((color >> 16) & 0xFF) / 255f;
        var a = ((color >> 24) & 0xFF) / 255f;
        return new Vector4(r, g, b, a);
    }

    /// <summary>Convert a Vector4 (from an ImGui color picker) back to a packed ImGui color.</summary>
    public static uint FromVector4(Vector4 v)
    {
        var r = (uint)(Math.Clamp(v.X, 0f, 1f) * 255f);
        var g = (uint)(Math.Clamp(v.Y, 0f, 1f) * 255f);
        var b = (uint)(Math.Clamp(v.Z, 0f, 1f) * 255f);
        var a = (uint)(Math.Clamp(v.W, 0f, 1f) * 255f);
        return (a << 24) | (b << 16) | (g << 8) | r;
    }

    /// <summary>Return <paramref name="color" /> with its alpha replaced by <paramref name="alpha" />.</summary>
    public static uint WithAlpha(uint color, byte alpha)
    {
        return (color & 0x00FFFFFF) | ((uint)alpha << 24);
    }

    /// <summary>Return <paramref name="color" /> with its alpha scaled by <paramref name="factor" /> (0..1).</summary>
    public static uint MultiplyAlpha(uint color, float factor)
    {
        var a = (uint)MathF.Round(((color >> 24) & 0xFF) * Math.Clamp(factor, 0f, 1f));
        return (color & 0x00FFFFFF) | (Math.Min(a, 255u) << 24);
    }

    // A small, deliberately FF-flavored default palette.
    public static readonly uint Black = FromHex("000000");
    public static readonly uint White = FromHex("FFFFFF");
    public static readonly uint Hp = FromHex("4CC94C");
    public static readonly uint Mp = FromHex("C964C9");
    public static readonly uint Shield = FromHex("E6E64C");

    /// <summary>Absorb-shield overlay on health bars: a bright, glowy blue.</summary>
    public static readonly uint ShieldBar = FromHex("66D9FF");
    public static readonly uint BarBackground = Rgba(0, 0, 0, 0xB4);
    public static readonly uint BarBorder = Rgba(0, 0, 0, 0xFF);
}
