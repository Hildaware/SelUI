using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SelUI.Game;

/// <summary>Game interactions triggered from clickable frames.</summary>
public static class UnitInteraction
{
    /// <summary>Open the standard right-click context menu (with its submenus) for the given actor.</summary>
    public static unsafe void OpenContextMenu(IGameObject actor)
    {
        var module = AgentModule.Instance();
        if (module == null) return;

        var hud = module->GetAgentHUD();
        if (hud == null) return;

        hud->OpenContextMenuFromTarget((CSGameObject*)actor.Address);
    }
}
