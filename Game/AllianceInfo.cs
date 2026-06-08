using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace SelUI.Game;

/// <summary>
///     Helpers for identifying which alliance (A / B / C) a 24-man raid member belongs to. The game
///     tracks this per-member in <see cref="InfoProxyCrossRealm" /> (group index 0/1/2), which is populated
///     for alliance raids regardless of whether they're actually cross-world.
/// </summary>
public static unsafe class AllianceInfo
{
    /// <summary>Alliance letter ('A'/'B'/'C') for the member with this content id, or '\0' if unknown.</summary>
    public static char GroupLetter(ulong contentId)
    {
        if (contentId == 0) return '\0';

        var member = InfoProxyCrossRealm.GetMemberByContentId(contentId);
        if (member == null) return '\0';

        return member->GroupIndex < 3 ? (char)('A' + member->GroupIndex) : '\0';
    }
}
