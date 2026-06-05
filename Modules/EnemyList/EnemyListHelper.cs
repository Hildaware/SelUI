using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SelUI.Modules.EnemyList;

/// <summary>
///     Reads the game's enemy list (the aggro'd-enemies HUD): each entry's entity id plus its enmity
///     level (0–4). Enmity is read off the native "_EnemyList" addon, the way DelvUI does it.
/// </summary>
public sealed unsafe class EnemyListHelper
{
    private readonly List<(uint EntityId, int Enmity)> _enemies = new();
    private readonly IGameGui _gameGui;

    public EnemyListHelper(IGameGui gameGui)
    {
        _gameGui = gameGui;
    }

    public IReadOnlyList<(uint EntityId, int Enmity)> Enemies => _enemies;

    public void Update()
    {
        _enemies.Clear();

        var array = EnemyListNumberArray.Instance();
        if (array == null) return;

        // Count isn't cleanly exposed; it lives just after the header (matches DelvUI).
        var count = *(int*)((byte*)array + 0x04);
        if (count <= 0) return;

        for (var i = 0; i < count && i < 8; i++)
        {
            var entityId = (uint)array->Enemies[i].EntityId;
            if (entityId is 0 or 0xE0000000) continue;
            _enemies.Add((entityId, GetEnmityLevel(i)));
        }
    }

    private int GetEnmityLevel(int index)
    {
        var addon = (AtkUnitBase*)_gameGui.GetAddonByName("_EnemyList", 1).Address;
        if (addon == null || addon->RootNode == null) return 0;

        var id = index == 0 ? 2 : 20000 + index; // node id scheme used by the addon
        var node = addon->GetNodeById((uint)id);
        if (node == null || node->GetComponent() == null) return 0;

        var image = (AtkImageNode*)node->GetComponent()->UldManager.SearchNodeById(13);
        if (image == null) return 0;

        return Math.Min(4, image->PartId + 1);
    }
}
