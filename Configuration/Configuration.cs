using Dalamud.Configuration;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Plugin;
using SelUI.Modules.CastBar;
using SelUI.Modules.EnemyList;
using SelUI.Modules.Nameplates;
using SelUI.Modules.Party;
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

    /// <summary>Global default font for all text. Null = the bundled Miedinger font.</summary>
    public SingleFontSpec? Font { get; set; }

    // --- Module configs ---
    public UnitFrameConfig PlayerUnitFrame { get; set; } = UnitFrameConfig.PlayerDefault();
    public UnitFrameConfig TargetUnitFrame { get; set; } = UnitFrameConfig.TargetDefault();
    public CastBarConfig CastBar { get; set; } = new();
    public PartyFramesConfig PartyFrames { get; set; } = new();
    public EnemyListConfig EnemyList { get; set; } = new();
    public NameplatesConfig Nameplates { get; set; } = new();

    public int Version { get; set; } = 9;

    public void Save(IDalamudPluginInterface pi)
    {
        pi.SavePluginConfig(this);
    }
}
