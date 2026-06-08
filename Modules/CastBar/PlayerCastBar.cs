using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using SelUI.Rendering;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaItem = Lumina.Excel.Sheets.Item;
using LuminaMount = Lumina.Excel.Sheets.Mount;

namespace SelUI.Modules.CastBar;

/// <summary>
///     The player's cast bar, drawn independently of the player unit frame. While the player casts it
///     shows a progress bar with the spell name above it (left-aligned to the bar) and the spell's
///     icon docked to the left — a square sized to span the name + bar block. Interruptible casts use a
///     red fill. The bar fades in/out and lingers briefly on completion.
/// </summary>
public sealed unsafe class PlayerCastBar : IHudModule, IMovableModule
{
    private const float FadeDuration = 0.15f;     // seconds to fade fully in or out
    private const float Margin = 12f;             // glow-bloom breathing room around the content
    private const float IconOffsetX = 4f;         // baked nudge of the spell icon further left of the bar
    private static readonly uint CastColor = Colors.FromHex("D9A441");
    private static readonly uint InterruptColor = Colors.FromHex("E05A5A");

    private const string NativeCastBar = "_CastBar"; // the game's own cast bar addon
    private static readonly Vector2 OffScreen = new(-9999f, -9999f);

    private readonly Func<IBattleChara?> _actorProvider;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly BarRenderer _bars;
    private readonly CastBarConfig _config;
    private readonly IDataManager _data;
    private readonly IGameGui _gameGui;
    private readonly Dictionary<uint, ISharedImmediateTexture> _iconCache = new();
    private readonly LabelRenderer _labels;
    private readonly RenderScale _scale;
    private readonly ITextureProvider _textures;
    private ExcelSheet<LuminaAction>? _actionSheet;

    // The native cast bar is moved off-screen (rather than toggled) while our module is enabled; we
    // remember its real position so we can put it back when we stop hiding it.
    private bool _nativeHidden;
    private Vector2 _nativeCastBarPos;

    // Last live cast, snapshotted so the bar can keep rendering through its fade-out.
    private float _alpha;
    private uint _castActionId;
    private byte _castActionType;
    private float _castCurrent;
    private float _castTotal;
    private bool _interruptible;

    public PlayerCastBar(CastBarConfig config, Func<IBattleChara?> actorProvider, BarRenderer bars, LabelRenderer labels,
        ITextureProvider textures, IDataManager data, IGameGui gameGui, IAddonLifecycle addonLifecycle, RenderScale scale)
    {
        _config = config;
        _actorProvider = actorProvider;
        _bars = bars;
        _labels = labels;
        _textures = textures;
        _data = data;
        _gameGui = gameGui;
        _addonLifecycle = addonLifecycle;
        _scale = scale;

        // Hook the native cast bar's PreDraw and shove its root node off-screen every
        // frame, so it never fights us re-asserting its position. Restored on disable / unload.
        _addonLifecycle.RegisterListener(AddonEvent.PreDraw, NativeCastBar, OnNativeCastBarPreDraw);
    }

    public string Name => "Player Cast Bar";

    public ModuleConfig Config => _config;

    public string EditLabel => Name;

    // The visual block extends left of Position (the icon) and above it (the name row); mirror the
    // layout math in Draw so the box matches the rendered cast bar.
    public Vector2 EditTopLeft =>
        _config.Position - new Vector2(_labels.Scale(_config.NameFontSize) + (_config.BarHeight + IconOffsetX) * _scale.Value, _labels.Scale(_config.NameFontSize));

    public Vector2 EditSize =>
        new(_labels.Scale(_config.NameFontSize) + (_config.BarHeight + IconOffsetX + _config.Width) * _scale.Value,
            _config.BarHeight * _scale.Value + _labels.Scale(_config.NameFontSize));

    public void MoveBy(Vector2 delta) => _config.Position += delta;

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PreDraw, NativeCastBar, OnNativeCastBarPreDraw);
        RestoreNativeCastBar();
    }

    public void Draw()
    {
        var player = _actorProvider();
        var casting = player is { IsCasting: true } && player.TotalCastTime > 0f;
        if (casting)
        {
            _castCurrent = player!.CurrentCastTime;
            _castTotal = player.TotalCastTime;
            _castActionId = player.CastActionId;
            _castActionType = player.CastActionType;
            _interruptible = player.IsCastInterruptible;
        }

        // Animate alpha toward casting / idle.
        var dt = ImGui.GetIO().DeltaTime;
        var step = FadeDuration > 0f ? dt / FadeDuration : 1f;
        var target = casting ? 1f : 0f;
        _alpha = target > _alpha ? MathF.Min(target, _alpha + step) : MathF.Max(target, _alpha - step);
        if (_alpha <= 0.001f) return;

        var cfg = _config;
        var (name, iconId) = CastAction(_castActionId, _castActionType);

        // Bar geometry grows with the game's UI scale; the anchor (Position) stays put.
        var v = _scale.Value;
        var barHeight = cfg.BarHeight * v;
        var barWidth = cfg.Width * v;
        var iconOffsetX = IconOffsetX * v;
        var margin = Margin * v;

        var nameH = _labels.Scale(cfg.NameFontSize); // the name row's height, scaled with the font
        var iconSize = nameH + barHeight; // square, spanning the name row + the bar
        var barPos = cfg.Position;

        // Window bounds: icon (left of the bar), the name row (above), the bar, plus glow margin. The
        // name can be wider than the bar, so extend the right edge to fit it.
        var nameWidth = _labels.Measure(name, cfg.NameFontSize).X;
        var left = barPos.X - iconSize - iconOffsetX;
        var top = barPos.Y - nameH;
        var right = barPos.X + MathF.Max(barWidth, nameWidth);
        var bottom = barPos.Y + barHeight;

        var windowPos = new Vector2(left - margin, top - margin);
        var windowSize = new Vector2(right - left + margin * 2f, bottom - top + margin * 2f);

        DrawHelper.DrawInWindow("SelUI_CastBar", windowPos, windowSize, false, dl =>
        {
            // Progress bar.
            var frac = _castTotal > 0f ? Math.Clamp(_castCurrent / _castTotal, 0f, 1f) : 0f;
            var color = _interruptible ? InterruptColor : CastColor;
            _bars.Draw(dl, barPos, new Vector2(barWidth, barHeight), Colors.BarBackground, frac, color,
                Colors.BarBorder, alpha: _alpha);

            // Remaining cast time, right-justified on the bar.
            var remaining = MathF.Max(0f, _castTotal - _castCurrent);
            _labels.Draw(dl, remaining.ToString("0.0"), new Vector2(barPos.X + barWidth - 4f * v, barPos.Y + barHeight / 2f),
                cfg.NameFontSize * 0.85f, Colors.White, DrawAnchor.Right, alpha: _alpha);

            // Spell name, above the bar and left-aligned to it.
            if (name.Length > 0)
                _labels.Draw(dl, name, new Vector2(barPos.X, barPos.Y), cfg.NameFontSize, Colors.White,
                    DrawAnchor.BottomLeft, alpha: _alpha);

            // Spell icon, a square docked to the left of the bar, its right edge on the bar's left edge.
            if (iconId != 0)
            {
                var wrap = GetIcon(iconId).GetWrapOrEmpty();
                var iconTopLeft = new Vector2(barPos.X - iconSize - iconOffsetX, barPos.Y - nameH);
                dl.AddImage(wrap.Handle, iconTopLeft, iconTopLeft + new Vector2(iconSize),
                    Vector2.Zero, Vector2.One, Colors.MultiplyAlpha(0xFFFFFFFFu, _alpha));
            }
        });
    }

    public bool DrawConfig()
    {
        var changed = false;

        var pos = _config.Position;
        if (ImGui.DragFloat2("Position", ref pos))
        {
            _config.Position = pos;
            changed = true;
        }

        // Width, bar height and name size are baked (see CastBarConfig + the load-time re-apply in Plugin).

        return changed;
    }

    /// <summary>
    ///     PreDraw hook on the game's own cast bar. While our module is enabled we move the addon's root
    ///     node off-screen (saving its real position first); when disabled we put it back. Runs every
    ///     frame the addon draws, so the game can't reclaim its spot mid-cast.
    /// </summary>
    private void OnNativeCastBarPreDraw(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || addon->RootNode == null) return;
        var node = addon->RootNode;

        if (_config.Enabled)
        {
            if (!_nativeHidden)
            {
                _nativeCastBarPos = new Vector2(node->GetXFloat(), node->GetYFloat());
                _nativeHidden = true;
            }

            node->SetPositionFloat(OffScreen.X, OffScreen.Y);
        }
        else if (_nativeHidden)
        {
            node->SetPositionFloat(_nativeCastBarPos.X, _nativeCastBarPos.Y);
            _nativeHidden = false;
        }
    }

    /// <summary>Put the native cast bar back where it was, if we'd moved it. Used on unload.</summary>
    private void RestoreNativeCastBar()
    {
        if (!_nativeHidden) return;

        var addon = (AtkUnitBase*)_gameGui.GetAddonByName(NativeCastBar, 1).Address;
        if (addon != null && addon->RootNode != null)
            addon->RootNode->SetPositionFloat(_nativeCastBarPos.X, _nativeCastBarPos.Y);
        _nativeHidden = false;
    }

    /// <summary>
    ///     The cast's name and icon. The id is looked up in the sheet that matches the cast type — mounts
    ///     and items don't live in the Action sheet — so e.g. mounting shows the correct mount icon.
    /// </summary>
    private (string name, uint iconId) CastAction(uint id, byte type)
    {
        if (id == 0) return (string.Empty, 0);
        try
        {
            switch ((ActionType)type)
            {
                case ActionType.Mount:
                {
                    var sheet = _data.GetExcelSheet<LuminaMount>();
                    if (sheet == null) return (string.Empty, 0);
                    var row = sheet.GetRow(id);
                    return (Capitalize(row.Singular.ExtractText()), row.Icon);
                }
                case ActionType.Item:
                {
                    var sheet = _data.GetExcelSheet<LuminaItem>();
                    if (sheet == null) return (string.Empty, 0);
                    var row = sheet.GetRow(id);
                    return (row.Name.ExtractText(), row.Icon);
                }
                default:
                {
                    _actionSheet ??= _data.GetExcelSheet<LuminaAction>();
                    if (_actionSheet == null) return (string.Empty, 0);
                    var row = _actionSheet.GetRow(id);
                    return (row.Name.ExtractText(), row.Icon);
                }
            }
        }
        catch
        {
            return (string.Empty, 0);
        }
    }

    /// <summary>Sheet names like mount singulars are lowercase; upper-case the first letter for display.</summary>
    private static string Capitalize(string s)
    {
        return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }

    private ISharedImmediateTexture GetIcon(uint iconId)
    {
        if (_iconCache.TryGetValue(iconId, out var tex)) return tex;
        tex = _textures.GetFromGameIcon(new GameIconLookup(iconId));
        _iconCache[iconId] = tex;
        return tex;
    }
}
