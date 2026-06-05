namespace SelUI.Modules.Nameplates;

/// <summary>
///     Nameplates module config. Appearance is baked per type (see <see cref="NameplateLayouts" />);
///     the only user settings are the module enable flag (from <see cref="ModuleConfig" />) and these
///     situational-behavior toggles. The behaviors' tuning (distance curves, icon size) stays baked.
/// </summary>
public sealed class NameplatesConfig : ModuleConfig
{
    /// <summary>Party members swap from name-only to a centered, enlarged job icon while in combat.</summary>
    public bool PartyJobIconInCombat { get; set; }

    /// <summary>Keep showing (otherwise target-only) player nameplates while in a city / resting area.</summary>
    public bool ShowPlayersInCities { get; set; }

    /// <summary>
    ///     Auto-show "important" NPCs/enemies — those the game tags with a nameplate marker (active quest
    ///     NPCs, and also shops / aetherytes / other service NPCs). FATE mobs you've joined always show
    ///     regardless of this. Off by default so it doesn't flood cities with plates.
    /// </summary>
    public bool ShowImportantNpcs { get; set; }

    /// <summary>Fade nameplates out as they get farther from the player.</summary>
    public bool FadeByDistance { get; set; }

    /// <summary>Shrink nameplates as they get farther from the player.</summary>
    public bool ScaleByDistance { get; set; }
}
