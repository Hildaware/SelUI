using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.Types;
using SelUI.Game;
using SelUI.Modules;
using SelUI.Modules.Alliance;
using SelUI.Modules.BarBuilder;
using SelUI.Modules.CastBar;
using SelUI.Modules.EnemyList;
using SelUI.Modules.Nameplates;
using SelUI.Modules.Party;
using SelUI.Modules.Statuses;
using SelUI.Modules.UnitFrames;
using SelUI.Rendering;
using SelUI.UI;

namespace SelUI;

// ReSharper disable once UnusedType.Global
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/selui";

    private readonly ICommandManager _commandManager;
    private readonly ConfigWindow _configWindow;
    private readonly EditModeOverlay _editOverlay;
    private readonly FontManager _fontManager;
    private readonly HudManager _hudManager;
    private readonly MouseoverManager _mouseover;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly RenderScale _renderScale;
    private readonly WindowSystem _windowSystem;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IPartyList partyList,
        IBuddyList buddyList,
        ICondition condition,
        IGameGui gameGui,
        ITextureProvider textureProvider,
        IDataManager dataManager,
        IAddonLifecycle addonLifecycle,
        IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;

        var config = pluginInterface.GetPluginConfig() as Configuration.Configuration ?? new Configuration.Configuration();

        // Pre-release: config is still at v1, so there are no released versions to migrate from. Add
        // `if (config.Version < N)` one-time-default blocks here once we ship and need to nudge persisted
        // state on existing installs.

        // Row appearance for the list frames is baked design, not user config — re-apply from code on
        // every load (the module Position/spacing stays user-configurable on the parent config).
        config.PartyFrames.Row = UnitFrameConfig.PartyRowDefault();
        config.EnemyList.Row = UnitFrameConfig.EnemyRowDefault();

        // The target frame's appearance is baked too; only its placement, width and hide-in-combat
        // toggle stay user state, so re-apply the baked defaults around those on every load.
        var bakedTarget = UnitFrameConfig.TargetDefault();
        bakedTarget.Position = config.TargetUnitFrame.Position;
        bakedTarget.Width = config.TargetUnitFrame.Width;
        bakedTarget.HideInCombat = config.TargetUnitFrame.HideInCombat;
        config.TargetUnitFrame = bakedTarget;

        // The player frame's appearance is baked too; only its placement and width stay user state, so
        // re-apply the baked defaults around those on every load.
        var bakedPlayer = UnitFrameConfig.PlayerDefault();
        bakedPlayer.Position = config.PlayerUnitFrame.Position;
        bakedPlayer.Width = config.PlayerUnitFrame.Width;
        bakedPlayer.HideOutOfCombat = config.PlayerUnitFrame.HideOutOfCombat;
        bakedPlayer.HideWhenFullHealth = config.PlayerUnitFrame.HideWhenFullHealth;
        config.PlayerUnitFrame = bakedPlayer;

        // The cast bar's size/look is baked; only its position (and enable toggle) stay user state.
        config.CastBar = new CastBarConfig { Position = config.CastBar.Position, Enabled = config.CastBar.Enabled };

        // Status layouts are baked design, not user config — apply them from code on every load.
        // The player's buffs/debuffs are their own module now (PlayerStatuses), so keep them off the
        // player frame (otherwise they'd draw twice).
        config.PlayerUnitFrame.Buffs = new StatusListConfig { Enabled = false };
        config.PlayerUnitFrame.Debuffs = new StatusListConfig { Enabled = false };
        config.TargetUnitFrame.Buffs = StatusLayouts.TargetBuffs();
        config.TargetUnitFrame.Debuffs = StatusLayouts.TargetDebuffs();
        config.PartyFrames.Row.Buffs = StatusLayouts.PartyBuffs();
        config.PartyFrames.Row.Debuffs = StatusLayouts.PartyDebuffs();
        config.EnemyList.Row.Buffs = StatusLayouts.EnemyBuffs();
        config.EnemyList.Row.Debuffs = StatusLayouts.EnemyDebuffs();

        // Rendering foundation. RenderScale is the user's global "Overall Scale" multiplier on every
        // baked size, seeded from the saved config and updated live from the slider in the config window.
        _renderScale = new RenderScale { Value = config.UiScale };
        _fontManager = new FontManager(pluginInterface);
        _fontManager.ActiveBundledFont = config.BundledFont ?? FontManager.DefaultBundledFont;
        var labels = new LabelRenderer(_fontManager, _renderScale) { GlobalFont = config.Font, GlobalScale = config.FontScale };
        var bars = new BarRenderer(_renderScale);
        var statuses = new StatusRenderer(labels, textureProvider, objectTable, _renderScale);
        var unitFrame = new UnitFrame(bars, labels, textureProvider, dataManager, statuses, objectTable, _renderScale);
        _mouseover = new MouseoverManager();

        // Sample status icons for the party preview: a few real buffs and a few cleansable debuffs.
        var (mockBuffIcons, mockDebuffIcons) = BuildMockStatusIcons(dataManager);

        // Modules — add new ones here and they appear in the HUD and config window automatically.
        // Every unit frame is the same module with a different actor source.
        var modules = new List<IHudModule>
        {
            new UnitFrameModule("Player Frame", "SelUI_Player", config.PlayerUnitFrame,
                () => objectTable.LocalPlayer, unitFrame,
                onLeftClick: actor => targetManager.Target = actor,
                onRightClick: UnitInteraction.OpenContextMenu,
                onHover: _mouseover.SetHovered,
                inCombat: () => ActorState.InCombat(objectTable.LocalPlayer),
                appearanceConfigurable: false,
                hideOptions: FrameHideOptions.OutOfCombat | FrameHideOptions.FullHealth),
            new UnitFrameModule("Target Frame", "SelUI_Target", config.TargetUnitFrame,
                () => targetManager.Target, unitFrame,
                onLeftClick: actor => targetManager.Target = actor,
                onRightClick: UnitInteraction.OpenContextMenu,
                onHover: _mouseover.SetHovered,
                inCombat: () => ActorState.InCombat(objectTable.LocalPlayer),
                markerProvider: FateHelper.MarkerFor,
                appearanceConfigurable: false,
                hideOptions: FrameHideOptions.InCombat),
            new PlayerCastBar(config.CastBar, () => objectTable.LocalPlayer as IBattleChara, bars, labels, textureProvider, dataManager, gameGui, addonLifecycle, _renderScale),
            new PlayerStatuses(config.PlayerStatuses, () => objectTable.LocalPlayer as IBattleChara, statuses,
                StatusLayouts.PlayerPermanentStatuses(), StatusLayouts.PlayerBuffs(), StatusLayouts.PlayerDebuffs(), _renderScale),
            new PartyFrames(config.PartyFrames, partyList, buddyList, objectTable, targetManager, _mouseover, unitFrame,
                mockBuffIcons, mockDebuffIcons, _renderScale),
            new AllianceFrames(config.Alliance, partyList, unitFrame, labels, _renderScale),
            new EnemyList(config.EnemyList, new EnemyListHelper(gameGui), objectTable, targetManager, _mouseover,
                unitFrame, textureProvider, mockDebuffIcons, _renderScale),
            new Nameplates(config.Nameplates, objectTable, targetManager, gameGui, condition, unitFrame, _renderScale)
            // TEMPORARY dev tool: visual bar tuner — uncomment to bring it back in the config window / HUD.
            // , new BarBuilder(config.BarBuilder)
        };

        _hudManager = new HudManager(modules, () => config.Enabled, clientState, condition, log);

        var editState = new EditModeState();

        // Movable edit boxes: every IMovableModule, plus any sub-boxes a module hosts (the Statuses
        // module places its buffs and debuffs grids independently).
        var movables = modules.OfType<IMovableModule>()
            .Concat(modules.OfType<IMovableModuleHost>().SelectMany(h => h.MovableParts))
            .ToList();
        _editOverlay = new EditModeOverlay(movables, labels, editState, () => config.Save(pluginInterface));

        _configWindow = new ConfigWindow(config, pluginInterface, _fontManager, labels, _renderScale, _hudManager.Modules, editState);
        _windowSystem = new WindowSystem("SelUI");
        _windowSystem.AddWindow(_configWindow);

        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        pluginInterface.UiBuilder.Draw += _hudManager.Draw;
        pluginInterface.UiBuilder.Draw += _editOverlay.Draw; // after the HUD, so boxes sit on top
        pluginInterface.UiBuilder.Draw += _mouseover.Apply; // after the HUD draws, so hover is recorded
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
        pluginInterface.UiBuilder.OpenMainUi += OpenConfigUi;

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the SelUI settings window."
        });
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler(CommandName);
        _pluginInterface.UiBuilder.OpenMainUi -= OpenConfigUi;
        _pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        _pluginInterface.UiBuilder.Draw -= _mouseover.Apply;
        _pluginInterface.UiBuilder.Draw -= _editOverlay.Draw;
        _pluginInterface.UiBuilder.Draw -= _hudManager.Draw;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

        _hudManager.Dispose();
        _mouseover.Dispose();
        _fontManager.Dispose();
    }

    private static (List<uint> buffs, List<uint> debuffs) BuildMockStatusIcons(IDataManager dataManager)
    {
        var buffs = new List<uint>();
        var debuffs = new List<uint>();
        try
        {
            var sheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            if (sheet != null)
                foreach (var s in sheet)
                {
                    if (s.Icon == 0) continue;
                    if (s.StatusCategory == 1)
                    {
                        if (buffs.Count < 5) buffs.Add(s.Icon);
                    }
                    else if (s.CanDispel)
                    {
                        if (debuffs.Count < 5) debuffs.Add(s.Icon);
                    }

                    if (buffs.Count >= 5 && debuffs.Count >= 5) break;
                }
        }
        catch
        {
            // Preview icons are non-essential; ignore sheet issues.
        }

        return (buffs, debuffs);
    }

    private void OnCommand(string command, string args)
    {
        _configWindow.Toggle();
    }

    private void OpenConfigUi()
    {
        _configWindow.Toggle();
    }
}
