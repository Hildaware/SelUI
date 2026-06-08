namespace SelUI.Modules;

/// <summary>
///     Shared layout bits for the vertically-stacked list frames (party, enemy list): the row spacing
///     math and the grow up/down combo options, so each module doesn't reimplement them.
/// </summary>
public static class ListLayout
{
    /// <summary>Grow-direction combo options — index 0 = down, 1 = up (matches <c>GrowUp</c>).</summary>
    public static readonly string[] GrowthItems = ["Down", "Up"];

    /// <summary>Top-left of row <paramref name="index" />, stacking up or down from <paramref name="position" />.</summary>
    public static Vector2 RowOrigin(Vector2 position, bool growUp, float pitch, int index) =>
        position + new Vector2(0f, (growUp ? -1f : 1f) * index * pitch);
}
