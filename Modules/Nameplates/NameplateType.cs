namespace SelUI.Modules.Nameplates;

/// <summary>
///     The kind of actor a nameplate is attached to. Each type gets its own baked layout (see
///     <see cref="NameplateLayouts" />); later these will gain sub-states (in combat, etc.).
/// </summary>
public enum NameplateType
{
    Enemy,
    Player,
    PartyMember,
    AllianceMember,
    Friend,
    Pet,
    Npc,
    Minion,
    Object
}
