namespace SelUI.Game;

/// <summary>Job-role classification by ClassJob row id (baked; the game's role enum isn't needed here).</summary>
public static class JobRoles
{
    // CNJ, WHM, SCH, AST, SGE.
    private static readonly HashSet<uint> Healers = [6, 24, 28, 33, 40];

    public static bool IsHealer(uint jobId)
    {
        return Healers.Contains(jobId);
    }
}
