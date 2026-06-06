using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using SelUI.Rendering;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SelUI.Game;

/// <summary>
///     Picks the health-bar color for an actor. Players are colored by job; NPCs by disposition
///     (hostile / friendly / neutral). Job palette ported from BetterBags;
/// </summary>
public static class UnitColors
{
    public static readonly uint Friendly = Colors.Rgba(99, 172, 14);
    public static readonly uint Hostile = Colors.Rgba(233, 4, 4);
    public static readonly uint Neutral = Colors.Rgba(218, 157, 46);

    // ClassJob row id -> color. Ported from BetterBags' JobColors.
    private static readonly Dictionary<byte, Vector4> JobMap = new()
    {
        // Tanks
        [1] = new Vector4(0.659f, 0.824f, 0.902f, 1f),  // GLA
        [19] = new Vector4(0.659f, 0.824f, 0.902f, 1f), // PLD
        [3] = new Vector4(0.812f, 0.149f, 0.129f, 1f),  // MRD
        [21] = new Vector4(0.812f, 0.149f, 0.129f, 1f), // WAR
        [32] = new Vector4(0.820f, 0.149f, 0.800f, 1f), // DRK
        [37] = new Vector4(0.475f, 0.427f, 0.188f, 1f), // GNB

        // Healers
        [6] = new Vector4(1.000f, 0.941f, 0.863f, 1f),  // CNJ
        [24] = new Vector4(1.000f, 0.941f, 0.863f, 1f), // WHM
        [28] = new Vector4(0.525f, 0.341f, 1.000f, 1f), // SCH
        [33] = new Vector4(1.000f, 0.906f, 0.290f, 1f), // AST
        [40] = new Vector4(0.565f, 0.690f, 1.000f, 1f), // SGE

        // Melee DPS
        [2] = new Vector4(0.839f, 0.612f, 0.000f, 1f),  // PGL
        [20] = new Vector4(0.839f, 0.612f, 0.000f, 1f), // MNK
        [4] = new Vector4(0.255f, 0.392f, 0.804f, 1f),  // LNC
        [22] = new Vector4(0.255f, 0.392f, 0.804f, 1f), // DRG
        [29] = new Vector4(0.686f, 0.098f, 0.392f, 1f), // ROG
        [30] = new Vector4(0.686f, 0.098f, 0.392f, 1f), // NIN
        [34] = new Vector4(0.894f, 0.427f, 0.016f, 1f), // SAM
        [39] = new Vector4(0.588f, 0.353f, 0.565f, 1f), // RPR
        [41] = new Vector4(0.063f, 0.510f, 0.063f, 1f), // VPR

        // Ranged Physical DPS
        [5] = new Vector4(0.569f, 0.729f, 0.369f, 1f),  // ARC
        [23] = new Vector4(0.569f, 0.729f, 0.369f, 1f), // BRD
        [31] = new Vector4(0.431f, 0.882f, 0.839f, 1f), // MCH
        [38] = new Vector4(0.886f, 0.690f, 0.686f, 1f), // DNC

        // Casters
        [7] = new Vector4(0.647f, 0.475f, 0.839f, 1f),  // THM
        [25] = new Vector4(0.647f, 0.475f, 0.839f, 1f), // BLM
        [26] = new Vector4(0.176f, 0.608f, 0.471f, 1f), // ACN
        [27] = new Vector4(0.176f, 0.608f, 0.471f, 1f), // SMN
        [35] = new Vector4(0.910f, 0.482f, 0.482f, 1f), // RDM
        [36] = new Vector4(0.000f, 0.725f, 0.969f, 1f), // BLU
        [42] = new Vector4(0.988f, 0.573f, 0.882f, 1f)  // PCT
    };

    /// <summary>Health-bar color for the given actor (job color for players, disposition color for NPCs).</summary>
    public static uint ForActor(IGameObject? actor)
    {
        if (actor is not ICharacter character) return Neutral;

        // Players (and the player's chocobo companion) use job color.
        if (character.ObjectKind == ObjectKind.Pc ||
            (character.SubKind == 9 && character.ClassJob.RowId > 0))
            return Job(character.ClassJob.RowId);

        var hostile = IsHostile(actor);

        if (character is IBattleNpc npc)
        {
            if ((npc.BattleNpcKind == BattleNpcSubKind.Combatant || npc.BattleNpcKind == BattleNpcSubKind.BNpcPart) && hostile)
                return Hostile;
            return Friendly;
        }

        return hostile ? Neutral : Friendly;
    }

    /// <summary>Color for a job's ClassJob row id, falling back to the default HP green for unknown jobs.</summary>
    public static uint Job(uint jobId)
    {
        return JobMap.TryGetValue((byte)jobId, out var v) ? Colors.FromVector4(v) : Colors.Hp;
    }

    private static unsafe bool IsHostile(IGameObject obj)
    {
        var go = (CSGameObject*)obj.Address;
        if (go == null) return false;

        // Nameplate color type: 4-11 cover the various "can be attacked / engaged" states.
        var plateType = go->GetNamePlateColorType();
        return plateType is >= 4 and <= 11;
    }
}
