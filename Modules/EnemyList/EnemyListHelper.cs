using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SelUI.Modules.EnemyList;

/// <summary>
///     Reads the game's enemy list (the aggro'd-enemies HUD): each entry's entity id plus its enmity
///     level (0–4). Enmity is read off the native "_EnemyList" addon.
/// </summary>
public sealed unsafe class EnemyListHelper
{
    private readonly List<(uint EntityId, int Enmity)> _enemies = new();
    private readonly IGameGui _gameGui;

    // The threat icons aren't a uniform grid — each enmity level is a separate ULD part with its own
    // (U, V, W, H). We read the real rect off the native addon's image node so our copy samples exactly
    // what the game does, instead of assuming evenly-sized cells. Indexed by enmity (1..4); cached across
    // frames so it survives once seen (and is available to the preview).
    private readonly Vector4?[] _partRects = new Vector4?[5];

    public EnemyListHelper(IGameGui gameGui)
    {
        _gameGui = gameGui;
    }

    public IReadOnlyList<(uint EntityId, int Enmity)> Enemies => _enemies;

    /// <summary>The native ULD part rect (U, V, W, H, in 1x texture pixels) for an enmity level, if known.</summary>
    public Vector4? PartRect(int enmity) => enmity is >= 1 and <= 4 ? _partRects[enmity] : null;

    public void Update()
    {
        _enemies.Clear();

        var array = EnemyListNumberArray.Instance();
        if (array == null) return;

        // Count isn't cleanly exposed; it lives just after the header.
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

        var enmity = Math.Min(4, image->PartId + 1);

        // Cache the real part rect for this enmity so the icon samples the game's own UVs.
        var partsList = image->PartsList;
        if (partsList != null && image->PartId < partsList->PartCount)
        {
            var part = partsList->Parts[image->PartId];
            _partRects[enmity] = new Vector4(part.U, part.V, part.Width, part.Height);
        }

        return enmity;
    }
}
