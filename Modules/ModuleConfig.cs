namespace SelUI.Modules;

/// <summary>
///     Base configuration shared by every HUD module. Concrete modules subclass this to add their own
///     settings. Keeping the enable flag here is what makes the plug-and-play toggle uniform.
/// </summary>
public abstract class ModuleConfig
{
    /// <summary>Whether this module draws. Toggled from the config window.</summary>
    public bool Enabled { get; set; } = true;
}
