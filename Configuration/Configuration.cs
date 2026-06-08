using Dalamud.Configuration;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Plugin;
using SelUI.Modules.Alliance;
using SelUI.Modules.BarBuilder;
using SelUI.Modules.CastBar;
using SelUI.Modules.EnemyList;
using SelUI.Modules.Nameplates;
using SelUI.Modules.Party;
using SelUI.Modules.Statuses;
using SelUI.Modules.UnitFrames;

namespace SelUI.Configuration;

/// <summary>
///     Root SelUI configuration. Each module owns a nested config object; the global font lives here so
///     every text element shares one default. No reflection, no attribute trees — just plain properties.
/// </summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>Master switch. When false, no module draws.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Global default font for all text, picked from the system font chooser. Null = use the bundled
    ///     font named by <see cref="BundledFont" /> (Grotesk by default). A system font takes priority.
    /// </summary>
    public SingleFontSpec? Font { get; set; }

    /// <summary>Which bundled font (from Media/Fonts) to use when no system <see cref="Font" /> is picked. Null = Grotesk.</summary>
    public string? BundledFont { get; set; }

    /// <summary>Global multiplier applied to every font size. 1.0 = the baked sizes. Clamped to [0.75, 1.5] in the UI.</summary>
    public float FontScale { get; set; } = 1f;

    /// <summary>Global multiplier on every baked pixel size, so the whole HUD scales uniformly. 1.0 = the baked sizes.</summary>
    public float UiScale { get; set; } = 1f;

    // --- Module configs ---
    public UnitFrameConfig PlayerUnitFrame { get; set; } = UnitFrameConfig.PlayerDefault();
    public UnitFrameConfig TargetUnitFrame { get; set; } = UnitFrameConfig.TargetDefault();
    public CastBarConfig CastBar { get; set; } = new();
    public PlayerStatusesConfig PlayerStatuses { get; set; } = new();
    public PartyFramesConfig PartyFrames { get; set; } = new();
    public AllianceFramesConfig Alliance { get; set; } = new();
    public EnemyListConfig EnemyList { get; set; } = new();
    public NameplatesConfig Nameplates { get; set; } = new();

    /// <summary>TEMPORARY: the visual bar-tuner dev tool. Additive — remove with the BarBuilder module.</summary>
    public BarBuilderConfig BarBuilder { get; set; } = new();

    public int Version { get; set; } = 1;

    public void Save(IDalamudPluginInterface pi)
    {
        pi.SavePluginConfig(this);
    }
}
