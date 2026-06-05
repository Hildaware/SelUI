using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace SelUI.Rendering;

/// <summary>The point of an element a position refers to. Mirrors DelvUI's anchor convention.</summary>
public enum DrawAnchor
{
    Center = 0,
    Left,
    Right,
    Top,
    TopLeft,
    TopRight,
    Bottom,
    BottomLeft,
    BottomRight
}

/// <summary>Which way a bar fills from empty to full.</summary>
public enum BarDirection
{
    Right,
    Left,
    Up,
    Down
}

public static class DrawHelper
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoBackground |
        ImGuiWindowFlags.NoMove |
        ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoBringToFrontOnFocus;

    /// <summary>
    ///     Draw a HUD element inside an invisible, borderless ImGui window anchored at
    ///     <paramref name="pos" />. The window gives the element its own draw list (so layering is
    ///     predictable) and clips drawing to <paramref name="size" />. Pass <paramref name="needsInput" />
    ///     = true only for elements the user can click (e.g. clickable unit frames).
    /// </summary>
    public static void DrawInWindow(string id, Vector2 pos, Vector2 size, bool needsInput, Action<ImDrawListPtr> draw)
    {
        var flags = BaseFlags;
        if (!needsInput) flags |= ImGuiWindowFlags.NoInputs;

        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(size);

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin(id, flags))
            draw(ImGui.GetWindowDrawList());

        ImGui.End();
    }

    /// <summary>
    ///     Given the screen point an element is anchored to, return the top-left corner of a box of
    ///     <paramref name="size" /> so that <paramref name="anchor" /> lands on <paramref name="anchorPos" />.
    /// </summary>
    public static Vector2 GetAnchoredPosition(Vector2 anchorPos, Vector2 size, DrawAnchor anchor)
    {
        return anchor switch
        {
            DrawAnchor.Center => anchorPos - size / 2f,
            DrawAnchor.Left => anchorPos - new Vector2(0f, size.Y / 2f),
            DrawAnchor.Right => anchorPos - new Vector2(size.X, size.Y / 2f),
            DrawAnchor.Top => anchorPos - new Vector2(size.X / 2f, 0f),
            DrawAnchor.TopLeft => anchorPos,
            DrawAnchor.TopRight => anchorPos - new Vector2(size.X, 0f),
            DrawAnchor.Bottom => anchorPos - new Vector2(size.X / 2f, size.Y),
            DrawAnchor.BottomLeft => anchorPos - new Vector2(0f, size.Y),
            DrawAnchor.BottomRight => anchorPos - size,
            _ => anchorPos
        };
    }
}
