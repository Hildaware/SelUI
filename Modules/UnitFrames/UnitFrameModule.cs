using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using SelUI.UI;

namespace SelUI.Modules.UnitFrames;

/// <summary>
///     A HUD module that draws one unit frame. It is fully defined by its name, its config, and a
///     delegate that supplies the actor to draw — so "Player Frame" and "Target Frame" are the same
///     class with different actor providers. Party / enemy-list entries will reuse it the same way.
/// </summary>
public sealed class UnitFrameModule : IHudModule
{
    private readonly Func<IGameObject?> _actorProvider;
    private readonly UnitFrameConfig _config;
    private readonly UnitFrame _frame;
    private readonly Func<bool>? _inCombat;
    private readonly Func<IGameObject, uint>? _markerProvider;
    private readonly Action<IGameObject>? _onHover;
    private readonly Action<IGameObject>? _onLeftClick;
    private readonly Action<IGameObject>? _onRightClick;
    private readonly string _windowId;

    public UnitFrameModule(string name, string windowId, UnitFrameConfig config, Func<IGameObject?> actorProvider, UnitFrame frame,
        Action<IGameObject>? onLeftClick = null, Action<IGameObject>? onRightClick = null, Action<IGameObject>? onHover = null,
        Func<bool>? inCombat = null, Func<IGameObject, uint>? markerProvider = null)
    {
        Name = name;
        _windowId = windowId;
        _config = config;
        _actorProvider = actorProvider;
        _frame = frame;
        _onLeftClick = onLeftClick;
        _onRightClick = onRightClick;
        _onHover = onHover;
        _inCombat = inCombat;
        _markerProvider = markerProvider;
    }

    public string Name { get; }

    public ModuleConfig Config => _config;

    public void Draw()
    {
        // Suppressing passes a null actor (rather than skipping the draw) so the frame fades out cleanly.
        var suppressed = _config.HideInCombat && (_inCombat?.Invoke() ?? false);
        var actor = suppressed ? null : _actorProvider();
        var marker = actor != null ? _markerProvider?.Invoke(actor) ?? 0u : 0u;
        _frame.Draw(_windowId, _config, actor, onLeftClick: _onLeftClick, onRightClick: _onRightClick, onHover: _onHover,
            markerIcon: marker);
    }

    public bool DrawConfig()
    {
        var changed = UnitFrameConfigUI.Draw(_config);

        // "Hide in combat" is only meaningful where a combat source is wired (the target frame).
        if (_inCombat != null)
        {
            var hide = _config.HideInCombat;
            if (ImGui.Checkbox("Hide in combat", ref hide))
            {
                _config.HideInCombat = hide;
                changed = true;
            }
        }

        return changed;
    }

    public void Dispose()
    {
    }
}
