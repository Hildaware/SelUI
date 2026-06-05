namespace SelUI.Modules.UnitFrames;

/// <summary>
///     Stand-in data for rendering a unit frame without a live actor (used by config previews). Carries
///     just enough to drive the bars, name/level/job, and mock status icons.
/// </summary>
public sealed class PreviewUnit
{
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public uint JobId { get; init; }

    /// <summary>Explicit bar color; when null, the job color for <see cref="JobId" /> is used.</summary>
    public uint? Color { get; init; }

    public float HpFraction { get; init; } = 1f;
    public float MpFraction { get; init; } = 1f;
    public IReadOnlyList<uint> BuffIcons { get; init; } = Array.Empty<uint>();
    public IReadOnlyList<uint> DebuffIcons { get; init; } = Array.Empty<uint>();
}
