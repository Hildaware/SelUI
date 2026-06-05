using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.Types;
using SelUI.Game;
using SelUI.Modules;
using SelUI.Modules.CastBar;
using SelUI.Modules.EnemyList;
using SelUI.Modules.Nameplates;
using SelUI.Modules.Party;
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
    private readonly FontManager _fontManager;
    private readonly HudManager _hudManager;
    private readonly MouseoverManager _mouseover;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly WindowSystem _windowSystem;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IPartyList partyList,
        ICondition condition,
        IGameGui gameGui,
        ITextureProvider textureProvider,
        IDataManager dataManager,
        IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;

        var config = pluginInterface.GetPluginConfig() as Configuration.Configuration ?? new Configuration.Configuration();

        // Party frames are still being designed; reset their (auto-generated) config so new defaults apply.
        if (config.Version < 7)
        {
            config.PartyFrames = new PartyFramesConfig();
            config.Version = 7;
            config.Save(pluginInterface);
        }

        // One-time default: hide the target frame in combat (existing configs predate the flag).
        if (config.Version < 8)
        {
            config.TargetUnitFrame.HideInCombat = true;
            config.Version = 8;
            config.Save(pluginInterface);
        }

        // One-time default: the player cast bar is now its own module, so drop the inline one.
        if (config.Version < 9)
        {
            config.PlayerUnitFrame.ShowCastBar = false;
            config.Version = 9;
            config.Save(pluginInterface);
        }

        // Row appearance for the list frames is baked design, not user config — re-apply from code on
        // every load (the module Position/spacing stays user-configurable on the parent config).
        config.PartyFrames.Row = UnitFrameConfig.PartyRowDefault();
        config.EnemyList.Row = UnitFrameConfig.EnemyRowDefault();

        // Status layouts are baked design, not user config — apply them from code on every load.
        config.PlayerUnitFrame.Buffs = StatusLayouts.PlayerBuffs();
        config.PlayerUnitFrame.Debuffs = StatusLayouts.PlayerDebuffs();
        config.TargetUnitFrame.Buffs = StatusLayouts.TargetBuffs();
        config.TargetUnitFrame.Debuffs = StatusLayouts.TargetDebuffs();
        config.PartyFrames.Row.Buffs = StatusLayouts.PartyBuffs();
        config.PartyFrames.Row.Debuffs = StatusLayouts.PartyDebuffs();
        config.EnemyList.Row.Buffs = StatusLayouts.EnemyBuffs();
        config.EnemyList.Row.Debuffs = StatusLayouts.EnemyDebuffs();

        // Rendering foundation.
        _fontManager = new FontManager(pluginInterface);
        var labels = new LabelRenderer(_fontManager) { GlobalFont = config.Font };
        var bars = new BarRenderer();
        var statuses = new StatusRenderer(labels, textureProvider, objectTable);
        var unitFrame = new UnitFrame(bars, labels, textureProvider, dataManager, statuses);
        _mouseover = new MouseoverManager();

        // Sample status icons for the party preview: a few real buffs and a few cleansable debuffs.
        var (mockBuffIcons, mockDebuffIcons) = BuildMockStatusIcons(dataManager);

        // Modules — add new ones here and they appear in the HUD and config window automatically.
        // Every unit frame is the same module with a different actor source.
        var modules = new List<IHudModule>
        {
            new UnitFrameModule("Player Frame", "SelUI_Player", config.PlayerUnitFrame,
                () => objectTable.LocalPlayer, unitFrame),
            new UnitFrameModule("Target Frame", "SelUI_Target", config.TargetUnitFrame,
                () => targetManager.Target, unitFrame,
                onLeftClick: actor => targetManager.Target = actor,
                onRightClick: UnitInteraction.OpenContextMenu,
                onHover: _mouseover.SetHovered,
                inCombat: () => objectTable.LocalPlayer?.StatusFlags.HasFlag(StatusFlags.InCombat) ?? false,
                markerProvider: FateHelper.MarkerFor),
            new PlayerCastBar(config.CastBar, () => objectTable.LocalPlayer as IBattleChara, bars, labels, textureProvider, dataManager),
            new PartyFrames(config.PartyFrames, partyList, objectTable, targetManager, _mouseover, unitFrame, textureProvider,
                mockBuffIcons, mockDebuffIcons),
            new EnemyList(config.EnemyList, new EnemyListHelper(gameGui), objectTable, targetManager, _mouseover,
                unitFrame, textureProvider, mockDebuffIcons),
            new Nameplates(config.Nameplates, objectTable, targetManager, gameGui, condition, unitFrame)
        };

        _hudManager = new HudManager(modules, () => config.Enabled, clientState, log);

        _configWindow = new ConfigWindow(config, pluginInterface, _fontManager, labels, _hudManager.Modules);
        _windowSystem = new WindowSystem("SelUI");
        _windowSystem.AddWindow(_configWindow);

        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        pluginInterface.UiBuilder.Draw += _hudManager.Draw;
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
