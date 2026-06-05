using Dalamud.Plugin.Services;

namespace SelUI.Modules;

/// <summary>
///     Holds every registered HUD module and drives their per-frame rendering. A module draws only
///     when the master switch is on, the player is logged in, and that module's own enable flag is set.
/// </summary>
public sealed class HudManager : IDisposable
{
    private readonly IClientState _clientState;
    private readonly Func<bool> _masterEnabled;
    private readonly IPluginLog _log;
    private readonly List<IHudModule> _modules;

    public HudManager(IReadOnlyList<IHudModule> modules, Func<bool> masterEnabled, IClientState clientState, IPluginLog log)
    {
        _modules = [..modules];
        _masterEnabled = masterEnabled;
        _clientState = clientState;
        _log = log;
    }

    public IReadOnlyList<IHudModule> Modules => _modules;

    public void Dispose()
    {
        foreach (var module in _modules) module.Dispose();
    }

    /// <summary>Subscribe this to <c>UiBuilder.Draw</c>.</summary>
    public void Draw()
    {
        if (!_masterEnabled()) return;
        if (!_clientState.IsLoggedIn) return;

        foreach (var module in _modules)
        {
            if (!module.Config.Enabled) continue;

            try
            {
                module.Draw();
            }
            catch (Exception e)
            {
                _log.Error(e, $"SelUI module '{module.Name}' threw while drawing.");
            }
        }
    }
}
