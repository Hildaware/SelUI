namespace SelUI.Modules;

/// <summary>
///     A HUD module whose position can be dragged in edit mode. Implemented alongside
///     <see cref="IHudModule" /> by the movable frames (player, target, cast bar, party, enemy list);
///     world-space modules like nameplates don't implement it. The outline box is derived from config
///     geometry so it shows even when the module has no live actor.
/// </summary>
public interface IMovableModule
{
    /// <summary>Caption drawn on the edit-mode outline (typically the module name).</summary>
    string EditLabel { get; }

    /// <summary>Screen-space top-left corner of the outline box.</summary>
    Vector2 EditTopLeft { get; }

    /// <summary>Size of the outline box.</summary>
    Vector2 EditSize { get; }

    /// <summary>Shift the module's stored position by <paramref name="delta" /> (one drag step).</summary>
    void MoveBy(Vector2 delta);
}
