namespace SelUI.Modules;

/// <summary>
///     A module that contributes one or more independently-movable edit-mode boxes, rather than (or in
///     addition to) being a single <see cref="IMovableModule" /> itself. This lets one config-window
///     entry own several draggable sub-elements — e.g. the player <c>Statuses</c> module places its
///     buffs and debuffs grids independently. Collected alongside <see cref="IMovableModule" /> when the
///     edit overlay's box list is built (see Plugin).
/// </summary>
public interface IMovableModuleHost
{
    /// <summary>The movable sub-boxes this module exposes to HUD layout edit mode.</summary>
    IEnumerable<IMovableModule> MovableParts { get; }
}
