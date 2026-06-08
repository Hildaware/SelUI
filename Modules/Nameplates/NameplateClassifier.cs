using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace SelUI.Modules.Nameplates;

/// <summary>Determines a nameplate's <see cref="NameplateType" /> from its actor.</summary>
public static class NameplateClassifier
{
    public static NameplateType Classify(IGameObject obj)
    {
        switch (obj.ObjectKind)
        {
            case ObjectKind.Pc:
                var flags = (obj as ICharacter)?.StatusFlags ?? StatusFlags.None;
                if (flags.HasFlag(StatusFlags.Hostile)) return NameplateType.Enemy; // e.g. PvP enemy players
                if (flags.HasFlag(StatusFlags.PartyMember)) return NameplateType.PartyMember;
                if (flags.HasFlag(StatusFlags.AllianceMember)) return NameplateType.AllianceMember;
                if (flags.HasFlag(StatusFlags.Friend)) return NameplateType.Friend;
                return NameplateType.Player;

            case ObjectKind.BattleNpc:
                if (obj is IBattleNpc npc)
                {
                    // Buddy = the player's chocobo companion; treat it like a pet (owner-name title).
                    if (npc.BattleNpcKind is BattleNpcSubKind.Pet or BattleNpcSubKind.Buddy)
                        return NameplateType.Pet;
                    if (npc.BattleNpcKind is BattleNpcSubKind.Combatant or BattleNpcSubKind.BNpcPart)
                        return NameplateType.Enemy;
                }

                return NameplateType.Npc;

            case ObjectKind.EventNpc:
            case ObjectKind.Retainer:
                return NameplateType.Npc;

            case ObjectKind.Companion:
                return NameplateType.Minion;

            default:
                return NameplateType.Object;
        }
    }
}
