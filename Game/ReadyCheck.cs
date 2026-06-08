using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace SelUI.Game;

/// <summary>
///     Reads the game's live ready-check state (<see cref="AgentReadyCheck" />) and maps a member's
///     content id to the icon to overlay on their frame: a green check if they readied, a red cross if
///     they declined. Returns 0 (no overlay) when no ready check is running or the member hasn't decided.
/// </summary>
public static unsafe class ReadyCheck
{
    // Ready-check result icons (green check / red cross). If these ever look wrong in-game, only these
    // two ids need changing.
    public const uint ReadyIcon = 76574;
    public const uint NotReadyIcon = 76575;

    /// <summary>Icon to draw for the member with this content id, or 0 if nothing should be shown.</summary>
    public static uint IconFor(ulong contentId)
    {
        if (contentId == 0) return 0;

        var agent = AgentReadyCheck.Instance();
        // Only while a ready check is actually running — otherwise the agent holds stale results.
        if (agent == null || !((AgentInterface*)agent)->IsAgentActive()) return 0;

        foreach (ref readonly var entry in agent->ReadyCheckEntries)
        {
            if (entry.ContentId != contentId) continue;
            return entry.Status switch
            {
                ReadyCheckStatus.Ready => ReadyIcon,
                ReadyCheckStatus.NotReady => NotReadyIcon,
                _ => 0 // Unknown / AwaitingResponse / MemberNotPresent — no decision to show yet
            };
        }

        return 0;
    }
}
