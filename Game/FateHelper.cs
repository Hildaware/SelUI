using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SelUI.Game;

/// <summary>
///     Thin accessors over the game's FATE state: which FATE an actor belongs to, which FATE the player
///     is currently inside, and a FATE's marker icon. Used to mark FATE enemies and auto-show the
///     nameplates of enemies in the FATE the player has joined.
/// </summary>
public static unsafe class FateHelper
{
    /// <summary>The id of the FATE this actor belongs to, or 0 if it isn't a FATE actor.</summary>
    public static ushort FateId(IGameObject go)
    {
        return ((CSGameObject*)go.Address)->FateId;
    }

    /// <summary>The FATE the player is currently within (counts toward the objective), or 0.</summary>
    public static ushort CurrentFateId()
    {
        var fm = FateManager.Instance();
        return fm != null ? fm->GetCurrentFateId() : (ushort)0;
    }

    /// <summary>The marker icon of a FATE by id, or 0 if it can't be resolved.</summary>
    public static uint FateIcon(ushort fateId)
    {
        if (fateId == 0) return 0;
        var fm = FateManager.Instance();
        if (fm == null) return 0;
        var fate = fm->GetFateById(fateId);
        return fate != null ? fate->IconId : 0u;
    }

    /// <summary>The FATE marker icon for an actor, or 0 if it isn't part of a FATE.</summary>
    public static uint MarkerFor(IGameObject go)
    {
        return FateIcon(FateId(go));
    }
}
