using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace SelUI.Game;

/// <summary>Small shared queries about an actor's live game state, used across the HUD modules.</summary>
public static class ActorState
{
    /// <summary>Whether <paramref name="actor" /> is the player's current target (hard or gamepad soft target).</summary>
    public static bool IsSelected(ITargetManager targets, IGameObject? actor)
    {
        if (actor == null) return false;
        return (targets.Target != null && targets.Target.Address == actor.Address)
               || (targets.SoftTarget != null && targets.SoftTarget.Address == actor.Address);
    }

    /// <summary>Whether <paramref name="actor" /> is a character the game currently flags as in combat.</summary>
    public static bool InCombat(IGameObject? actor)
    {
        return (actor as ICharacter)?.StatusFlags.HasFlag(StatusFlags.InCombat) ?? false;
    }
}
