using SelUI.Modules.UnitFrames;

namespace SelUI.Modules.EnemyList;

/// <summary>Settings for the enemy list: a vertical stack of compact enemy frames.</summary>
public sealed class EnemyListConfig : ModuleConfig
{
    /// <summary>Top-left position of the first row.</summary>
    public Vector2 Position { get; set; } = new(1480f, 300f);

    /// <summary>Vertical pitch between rows.</summary>
    public float RowHeight { get; set; } = 50f;

    /// <summary>Maximum enemy rows to show.</summary>
    public int MaxRows { get; set; } = 8;

    /// <summary>Preview a full enemy list (mock enemies + debuffs) for positioning/styling.</summary>
    public bool PreviewMode { get; set; }

    /// <summary>Per-enemy frame appearance (position is driven by the row layout).</summary>
    public UnitFrameConfig Row { get; set; } = UnitFrameConfig.EnemyRowDefault();
}
