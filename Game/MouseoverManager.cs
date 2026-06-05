using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SelUI.Game;

/// <summary>
///     Bridges hovering a SelUI frame to the game's native mouseover so that <c>&lt;mo&gt;</c> macros and
///     mouseover-action settings resolve to the hovered unit. Frames record the hovered actor each
///     frame via <see cref="SetHovered" />; <see cref="Apply" /> then writes it to the game once per
///     frame. We only clear the game's mouseover when leaving our frames (never every frame), so the
///     game's own world/party-list mouseover keeps working when we're not involved.
/// </summary>
public sealed unsafe class MouseoverManager : IDisposable
{
    private bool _active;
    private IGameObject? _hovered;

    public void Dispose()
    {
        if (_active) Set(IntPtr.Zero);
    }

    /// <summary>Record the actor whose frame is hovered this frame. Last writer wins.</summary>
    public void SetHovered(IGameObject actor)
    {
        _hovered = actor;
    }

    /// <summary>Apply the recorded hover to the game's mouseover target. Call once per frame after the HUD draws.</summary>
    public void Apply()
    {
        if (_hovered is { GameObjectId: not 0 })
        {
            Set(_hovered.Address);
            _active = true;
        }
        else if (_active)
        {
            // We were driving the mouseover and nothing of ours is hovered now — release it once.
            Set(IntPtr.Zero);
            _active = false;
        }

        _hovered = null;
    }

    private void Set(IntPtr address)
    {
        var ui = Framework.Instance()->GetUIModule();
        if (ui == null) return;

        var pronoun = ui->GetPronounModule();
        if (pronoun == null) return;

        pronoun->UiMouseOverTarget = (CSGameObject*)address;
    }
}
