namespace SelUI.UI;

/// <summary>
///     Transient flag for HUD layout edit mode. Toggled from the config window, read by the edit-mode
///     overlay. Deliberately not persisted — edit mode always starts off on reload.
/// </summary>
public sealed class EditModeState
{
    public bool Active { get; set; }
}
