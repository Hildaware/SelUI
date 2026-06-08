namespace SelUI.Game;

/// <summary>
///     Maps a job's ClassJob row id to a game icon id. Uses the modern
///     colorful gradient job icons — falling back to the gold-framed set (62100 + id) for anything not
///     in the table.
/// </summary>
public static class JobIcons
{
    private const uint FramedBase = 62100;

    /// <summary>Icon shown for the player's chocobo companion (it has no player job icon of its own).</summary>
    public const uint Chocobo = 60311;

    // ClassJob row id -> colorful gradient icon id.
    private static readonly Dictionary<uint, uint> Colorful = new()
    {
        // Tanks
        [1] = 94022,  // GLA
        [3] = 94024,  // MRD
        [19] = 94079, // PLD
        [21] = 94081, // WAR
        [32] = 94123, // DRK
        [37] = 94130, // GNB

        // Melee DPS
        [2] = 92523,  // PGL
        [4] = 92525,  // LNC
        [29] = 92621, // ROG
        [20] = 92580, // MNK
        [22] = 92582, // DRG
        [30] = 92622, // NIN
        [34] = 92627, // SAM
        [39] = 92632, // RPR
        [41] = 92685, // VPR

        // Ranged physical DPS
        [5] = 92526,  // ARC
        [23] = 92583, // BRD
        [31] = 92625, // MCH
        [38] = 92631, // DNC

        // Ranged magical DPS
        [7] = 92529,  // THM
        [26] = 92530, // ACN
        [25] = 92585, // BLM
        [27] = 92586, // SMN
        [35] = 92628, // RDM
        [36] = 92629, // BLU
        [42] = 92686, // PCT

        // Healers
        [6] = 94528,  // CNJ
        [24] = 94584, // WHM
        [28] = 94587, // SCH
        [40] = 94633, // SGE
        [33] = 94624, // AST

        // Crafters
        [8] = 91031,  // CRP
        [9] = 91032,  // BSM
        [10] = 91033, // ARM
        [11] = 91034, // GSM
        [12] = 91035, // LTW
        [13] = 91036, // WVR
        [14] = 91037, // ALC
        [15] = 91038, // CUL

        // Gatherers
        [16] = 91039, // MIN
        [17] = 91040, // BOT
        [18] = 91041  // FSH
    };

    /// <summary>Colorful "Style 3" icon for the job, or the gold-framed fallback for unknown jobs.</summary>
    public static uint Colored(uint jobId)
    {
        return Colorful.TryGetValue(jobId, out var icon) ? icon : FramedBase + jobId;
    }
}
