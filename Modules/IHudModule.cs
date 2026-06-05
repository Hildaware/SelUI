namespace SelUI.Modules;

/// <summary>
///     A self-contained, independently toggleable piece of the HUD — a unit frame, party frames,
///     nameplates, and so on. Modules own their rendering and their settings UI; the
///     <see cref="HudManager" /> just decides when to call them.
/// </summary>
public interface IHudModule : IDisposable
{
    /// <summary>Human-readable name shown in the config window.</summary>
    string Name { get; }

    /// <summary>This module's configuration slice (at least an enable flag).</summary>
    ModuleConfig Config { get; }

    /// <summary>Render the module for this frame. Only called while the module is enabled.</summary>
    void Draw();

    /// <summary>Draw this module's settings into the config window. Return true if a setting changed.</summary>
    bool DrawConfig();
}
