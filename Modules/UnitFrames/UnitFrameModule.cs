using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using SelUI.UI;

namespace SelUI.Modules.UnitFrames;

/// <summary>Which "hide the frame" visibility toggles a <see cref="UnitFrameModule" /> exposes in its settings.</summary>
[Flags]
public enum FrameHideOptions
{
    None = 0,
    InCombat = 1,
    OutOfCombat = 2,
    FullHealth = 4
}

/// <summary>
///     A HUD module that draws one unit frame. It is fully defined by its name, its config, and a
///     delegate that supplies the actor to draw — so "Player Frame" and "Target Frame" are the same
///     class with different actor providers. Party / enemy-list entries will reuse it the same way.
/// </summary>
public sealed class UnitFrameModule : IHudModule, IMovableModule
{
    private readonly Func<IGameObject?> _actorProvider;
    private readonly bool _appearanceConfigurable;
    private readonly UnitFrameConfig _config;
    private readonly UnitFrame _frame;
    private readonly FrameHideOptions _hideOptions;
    private readonly Func<bool>? _inCombat;
    private readonly Func<IGameObject, uint>? _markerProvider;
    private readonly Action<IGameObject>? _onHover;
    private readonly Action<IGameObject>? _onLeftClick;
    private readonly Action<IGameObject>? _onRightClick;
    private readonly string _windowId;

    public UnitFrameModule(string name, string windowId, UnitFrameConfig config, Func<IGameObject?> actorProvider, UnitFrame frame,
        Action<IGameObject>? onLeftClick = null, Action<IGameObject>? onRightClick = null, Action<IGameObject>? onHover = null,
        Func<bool>? inCombat = null, Func<IGameObject, uint>? markerProvider = null, bool appearanceConfigurable = true,
        FrameHideOptions hideOptions = FrameHideOptions.None)
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
        _appearanceConfigurable = appearanceConfigurable;
        _hideOptions = hideOptions;
    }

    public string Name { get; }

    public ModuleConfig Config => _config;

    public string EditLabel => Name;
    public Vector2 EditTopLeft => _config.Position + _frame.MeasureEditBox(_config).Offset;
    public Vector2 EditSize => _frame.MeasureEditBox(_config).Size;
    public void MoveBy(Vector2 delta) => _config.Position += delta;

    public void Draw()
    {
        // A hide condition suppresses by passing a null actor (rather than skipping the draw) so the
        // frame fades out cleanly via UnitFrame's built-in fade — the same way the target frame does.
        var actor = _actorProvider();
        if (actor != null && ShouldHide(actor)) actor = null;

        var marker = actor != null ? _markerProvider?.Invoke(actor) ?? 0u : 0u;
        _frame.Draw(_windowId, _config, actor, onLeftClick: _onLeftClick, onRightClick: _onRightClick, onHover: _onHover,
            markerIcon: marker);
    }

    /// <summary>Whether an active hide condition should suppress (fade out) the frame this draw.</summary>
    private bool ShouldHide(IGameObject actor)
    {
        if (_inCombat != null && (_config.HideInCombat || _config.HideOutOfCombat))
        {
            var inCombat = _inCombat();
            if (_config.HideInCombat && inCombat) return true;
            if (_config.HideOutOfCombat && !inCombat) return true;
        }

        if (_config.HideWhenFullHealth && actor is ICharacter { MaxHp: > 0 } c && c.CurrentHp >= c.MaxHp)
            return true;

        return false;
    }

    public bool DrawConfig()
    {
        bool changed;
        if (_appearanceConfigurable)
        {
            changed = UnitFrameConfigUI.Draw(_config);
        }
        else
        {
            // Appearance is baked (see UnitFrameConfig.TargetDefault + StatusLayouts.Target*); only the
            // frame's placement and width stay user-configurable.
            changed = false;

            var pos = _config.Position;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.DragFloat2("Position", ref pos))
            {
                _config.Position = pos;
                changed = true;
            }

            var width = _config.Width;
            ImGui.SetNextItemWidth(160f);
            if (ImGui.DragFloat("Width", ref width, 1f, 60f, 800f, "%.0f"))
            {
                _config.Width = width;
                changed = true;
            }
        }

        // Visibility toggles this frame chooses to expose (the frame fades out while a toggle's
        // condition holds).
        if (_hideOptions.HasFlag(FrameHideOptions.InCombat))
            changed |= HideToggle("Hide in combat", () => _config.HideInCombat, v => _config.HideInCombat = v);
        if (_hideOptions.HasFlag(FrameHideOptions.OutOfCombat))
            changed |= HideToggle("Hide out of combat", () => _config.HideOutOfCombat, v => _config.HideOutOfCombat = v);
        if (_hideOptions.HasFlag(FrameHideOptions.FullHealth))
            changed |= HideToggle("Hide at full health", () => _config.HideWhenFullHealth, v => _config.HideWhenFullHealth = v);

        return changed;
    }

    private static bool HideToggle(string label, Func<bool> get, Action<bool> set)
    {
        var v = get();
        if (!ImGui.Checkbox(label, ref v)) return false;
        set(v);
        return true;
    }

    public void Dispose()
    {
    }
}
