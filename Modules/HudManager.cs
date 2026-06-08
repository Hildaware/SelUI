using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace SelUI.Modules;

/// <summary>
///     Holds every registered HUD module and drives their per-frame rendering. A module draws only
///     when the master switch is on, the player is logged in, and that module's own enable flag is set.
///     The whole HUD is also suppressed during loading screens (between-areas), cutscenes, NPC
///     interactions (quest dialogue, vendors, summoning bell), and the login screen so our elements
///     never bleed over those states.
/// </summary>
public sealed class HudManager : IDisposable
{
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly Func<bool> _masterEnabled;
    private readonly IPluginLog _log;
    private readonly List<IHudModule> _modules;

    public HudManager(IReadOnlyList<IHudModule> modules, Func<bool> masterEnabled, IClientState clientState, ICondition condition, IPluginLog log)
    {
        _modules = [..modules];
        _masterEnabled = masterEnabled;
        _clientState = clientState;
        _condition = condition;
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
        if (!_clientState.IsLoggedIn) return; // covers the login screen
        if (IsSuppressed()) return;

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

    /// <summary>
    ///     Loading screens (between-areas), cutscenes, and any NPC interaction (quest dialogue, vendors,
    ///     summoning bell) — hide the whole HUD during these.
    /// </summary>
    private bool IsSuppressed()
    {
        return _condition.Any(
            ConditionFlag.BetweenAreas,
            ConditionFlag.BetweenAreas51,
            ConditionFlag.OccupiedInCutSceneEvent,
            ConditionFlag.WatchingCutscene,
            ConditionFlag.WatchingCutscene78,
            ConditionFlag.OccupiedInQuestEvent,
            ConditionFlag.OccupiedInEvent,
            ConditionFlag.OccupiedSummoningBell);
    }
}
