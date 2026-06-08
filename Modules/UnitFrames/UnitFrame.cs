using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using SelUI.Game;
using SelUI.Rendering;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace SelUI.Modules.UnitFrames;

/// <summary>
///     Renders a single unit frame from any actor. The whole unit-frame family (player, target, party,
///     enemy list, nameplates) shares this one renderer — it draws whichever of the seven primitives the
///     config enables, pulling data from the actor as <see cref="ICharacter" /> (hp/mp/level/job) and
///     <see cref="IBattleChara" /> (cast).
/// </summary>
public sealed class UnitFrame
{
    private const float CastFontScale = 0.85f;
    private const float TitleFontDrop = 6f; // title (nameplate-only) sits a few px smaller than the name
    private const float TitleGap = -4f; // name<->title spacing; negative pulls the title in (font carries whitespace)

    // Name highlight: the FFXIV item-slot highlight (the "highlight" texture from BetterBags), drawn
    // behind a centered name so name-only nameplates read as more than floating text.
    // The _hr1 variant is 2x resolution; GetFromGame loads the exact path (no hr1 substitution), so the
    // source-px coords must be in _hr1 space (double the base coords). UVs are derived from the loaded
    // texture's real size at draw time, so the result is the same regardless of the player's HD setting.
    private const string NameBgTexturePath = "ui/uld/NamePlate_hr1.tex";
    private static readonly Vector4 NameBgSrc = new(0f, 0f, 264f, 32f); // highlight sub-rect (_hr1 source px): x, y, w, h
    private const float NameBgCap = 32f;       // horizontal 3-slice cap width (_hr1 source px) — keeps the rounded ends crisp
    private const float NameBgPadX = 24f;      // breathing room each side of the name
    private const float NameBgPadY = 10f;      // breathing room above/below the name (adds 2x to bar height)
    private const float NameBgOffsetY = 1f;    // vertical nudge of the highlight (positive = down)
    private const float NameBgOpacity = 0.5f;  // baked tint alpha
    private static readonly uint NameBgTint = Colors.FromHex("000000"); // multiplies the texture RGB (black = dark backing)
    private const float LabelGap = 6f;      // horizontal gap between the level and the name
    private const float LevelPadding = 16f; // gap between the bar's left edge and the level text
    private const float OverlapPad = 6f; // right padding when a bar (mana / cast) overlaps the health bar
    private const float ManaOverlapPad = 10f; // mana sits 4px further left than the cast bar when overlapping
    private const float FadeDuration = 0.25f; // seconds to fade a frame fully in or out

    // Range fade (opt-in via UnitFrameConfig.RangeFade): once the actor is past action range the frame
    // dims toward RangeMinAlpha over a short ramp and stops taking clicks.
    private const float RangeFadeDistance = 30f; // yalms — most heals / party utility reach this far
    private const float RangeFadeWidth = 3f;     // yalms of smooth ramp past the threshold

    /// <summary>Dimmed alpha for a frame whose unit is fully out of range (also used by list previews).</summary>
    public const float OutOfRangeAlpha = 0.35f;

    private const uint LeaderIconId = 61521; // party / alliance leader crown, drawn on top of the frame

    private readonly BarRenderer _bars;
    private readonly IDataManager _data;
    private readonly IconCache _icons;
    private readonly LabelRenderer _labels;
    private readonly IObjectTable _objects;
    private readonly RenderScale _scale;
    private readonly Dictionary<string, FrameState> _states = new();
    private readonly StatusRenderer _statuses;
    private readonly ITextureProvider _textures;
    private ExcelSheet<LuminaAction>? _actionSheet;
    private ISharedImmediateTexture? _nameBgTexture;

    public UnitFrame(BarRenderer bars, LabelRenderer labels, ITextureProvider textures, IDataManager data, StatusRenderer statuses, IObjectTable objects, RenderScale scale)
    {
        _bars = bars;
        _labels = labels;
        _textures = textures;
        _icons = new IconCache(textures);
        _data = data;
        _statuses = statuses;
        _objects = objects;
        _scale = scale;
    }

    /// <summary>
    ///     A clone of <paramref name="cfg" /> with its geometry (bar/name/icon pixel sizes and offsets)
    ///     multiplied by the global UI scale, so a frame grows/shrinks with the game's UI while staying
    ///     anchored at its (unscaled) <see cref="UnitFrameConfig.Position" />. Font sizes are left raw —
    ///     <see cref="LabelRenderer" /> applies the UI scale to text itself — as are pure ratios and the
    ///     status collections (<see cref="StatusRenderer" /> scales those). Returns the original instance
    ///     unchanged at the reference scale to avoid a needless clone.
    /// </summary>
    private UnitFrameConfig ScaleGeometry(UnitFrameConfig cfg)
    {
        var s = _scale.Value;
        if (MathF.Abs(s - 1f) < 0.0001f) return cfg;

        var c = cfg.Clone();
        c.Width *= s;
        c.HealthBarHeight *= s;
        c.ManaBarHeight *= s;
        c.CastBarHeight *= s;
        c.Gap *= s;
        c.JobIconSize *= s;
        c.JobIconOffsetX *= s;
        c.NameRightOfIconGap *= s;
        c.NameOffsetY *= s;
        c.HealthTextOffsetY *= s;
        return c;
    }

    /// <summary>
    ///     The header row's height: the taller of the level text and an above-the-bar name (the name
    ///     unless it sits beside the icon), or zero when neither shows. Shared by the measure helpers and
    ///     <see cref="Draw" /> so they stay in lockstep. Expects an already-<see cref="ScaleGeometry" />'d cfg.
    /// </summary>
    private float HeaderHeight(UnitFrameConfig cfg)
    {
        var nameAboveBar = cfg.ShowName && !cfg.NameRightOfIcon;
        var headerH = 0f;
        if (cfg.ShowLevel) headerH = MathF.Max(headerH, _labels.Scale(cfg.LevelFontSize));
        if (nameAboveBar) headerH = MathF.Max(headerH, _labels.Scale(cfg.NameFontSize));
        return headerH;
    }

    /// <summary>
    ///     The edit-mode outline for a frame, driven by config flags rather than a live snapshot (edit
    ///     mode has no actor). Starts from the bars' footprint — same vertical-layout rules as
    ///     <see cref="Draw" />; mana/cast add height only when they stack below the health bar — then
    ///     expands to cover the job icon's overhang (it straddles the bar's top-left), so the draggable
    ///     box matches the whole visible frame even when the name/level header is off. Returns the box's
    ///     top-left as an offset from the frame origin (≤ 0 where the icon overhangs) plus its size.
    /// </summary>
    public (Vector2 Offset, Vector2 Size) MeasureEditBox(UnitFrameConfig cfg)
    {
        cfg = ScaleGeometry(cfg);
        var headerH = HeaderHeight(cfg);

        var hpH = cfg.ShowHealthBar ? cfg.HealthBarHeight : 0f;
        var mpH = cfg.ShowManaBar && !cfg.ManaOverlapHealth ? cfg.ManaBarHeight : 0f;
        var castH = cfg.ShowCastBar && !cfg.CastOverlapHealth ? cfg.CastBarHeight : 0f;

        var hpY = headerH;
        var y = headerH;
        if (hpH > 0f) y += hpH;
        if (mpH > 0f) y += cfg.Gap + mpH;
        if (castH > 0f) y += cfg.Gap + castH;

        // Bars footprint from the frame origin.
        float left = 0f, top = 0f, right = cfg.Width, bottom = y;

        // Expand to include the job icon, which straddles the bar's top-left. Edit mode has no actor, so
        // assume the icon shows whenever it's enabled (mirrors Draw's non-docked icon placement).
        if (cfg.ShowJobIcon)
        {
            var iconLeftX = -cfg.JobIconSize / 2f + cfg.JobIconOffsetX;
            var iconCenterY = hpY + hpH * cfg.JobIconAnchorY + cfg.JobIconOffsetY * _scale.Value;
            var iconTop = iconCenterY - cfg.JobIconSize / 2f;
            left = MathF.Min(left, iconLeftX);
            top = MathF.Min(top, iconTop);
            right = MathF.Max(right, iconLeftX + cfg.JobIconSize);
            bottom = MathF.Max(bottom, iconTop + cfg.JobIconSize);
        }

        return (new Vector2(left, top), new Vector2(right - left, bottom - top));
    }

    /// <summary>
    ///     The Y offset, from the frame's origin, of the health bar's vertical center — by the same
    ///     header/scale rules as <see cref="Draw" />. Lets external decorations (e.g. the enemy-list threat
    ///     icon) align to the bar without duplicating the layout math.
    /// </summary>
    public float HealthBarCenterY(UnitFrameConfig cfg)
    {
        cfg = ScaleGeometry(cfg);
        var headerH = HeaderHeight(cfg);

        var hpH = cfg.ShowHealthBar ? cfg.HealthBarHeight : 0f;
        return headerH + hpH / 2f; // hpY == headerH in Draw's layout
    }

    public void Draw(string id, UnitFrameConfig cfg, IGameObject? actor, Vector2? positionOverride = null, bool selected = false,
        Action<IGameObject>? onLeftClick = null, Action<IGameObject>? onRightClick = null, Action<IGameObject>? onHover = null,
        PreviewUnit? preview = null, bool drawStatuses = true, bool fade = true, string? title = null, bool titleAbove = false,
        float alphaMultiplier = 1f, ImDrawListPtr? drawListOverride = null, uint markerIcon = 0, Vector3? rangePosition = null,
        bool leader = false, float leaderIconSize = 0f, Vector2 leaderIconOffset = default, bool anchorBarLine = false,
        uint colorOverride = 0, uint iconOverride = 0, uint readyCheckIcon = 0)
    {
        // Grow every baked size with the game's UI scale (positions stay where they're anchored). Hardcoded
        // pixel pads below go through S(); cfg geometry is pre-scaled here; fonts scale inside LabelRenderer.
        cfg = ScaleGeometry(cfg);
        float S(float px) => px * _scale.Value;

        var character = actor as ICharacter;
        var battle = actor as IBattleChara;
        var hasHealth = character is { MaxHp: > 0 };

        Snapshot snap;
        float alpha;
        if (fade)
        {
            // The frame is "live" when it has something worth showing. Preview always counts; otherwise
            // target-style frames require a health pool, so objects / empty targets fade out.
            var live = preview != null || (actor != null && (!cfg.HideWhenNoActor || hasHealth));

            if (!_states.TryGetValue(id, out var state))
            {
                state = new FrameState();
                _states[id] = state;
            }

            // Snapshot while live so we can keep rendering through the fade-out after the actor is gone.
            if (live) state.Last = preview != null ? Snapshot.Preview(cfg, preview) : Snapshot.From(cfg, actor!, character, battle);

            // Animate alpha toward the live/hidden target over FadeDuration.
            var dt = ImGui.GetIO().DeltaTime;
            var step = FadeDuration > 0f ? dt / FadeDuration : 1f;
            var targetAlpha = live ? 1f : 0f;
            state.Alpha = targetAlpha > state.Alpha
                ? MathF.Min(targetAlpha, state.Alpha + step)
                : MathF.Max(targetAlpha, state.Alpha - step);

            if (state.Last is not { } s || state.Alpha <= 0.001f) return;
            snap = s;
            alpha = state.Alpha;
        }
        else
        {
            // No persistent state (used by nameplates, where actors are transient). Draw live only.
            if (preview != null) snap = Snapshot.Preview(cfg, preview);
            else if (actor != null && (!cfg.HideWhenNoActor || hasHealth)) snap = Snapshot.From(cfg, actor, character, battle);
            else return;
            alpha = 1f;
        }

        // Caller-supplied overrides (e.g. the chocobo companion, which has no player job color/icon of its own).
        if (colorOverride != 0) snap = snap with { Color = colorOverride };
        if (iconOverride != 0) snap = snap with { IconOverride = iconOverride };

        // Range fade: opt-in frames (party / alliance) dim and stop taking clicks once the unit is past
        // action range. Measured the way the game does — horizontal centre distance minus both hitbox
        // radii. Live actors carry their own position; the alliance frames (no live actor) pass the
        // party-list position via rangePosition so out-of-range members still dim.
        var inRange = true;
        var rangePos = actor?.Position ?? rangePosition;
        if (cfg.RangeFade && rangePos is { } unitPos)
        {
            var self = _objects.LocalPlayer;
            if (self != null && (actor == null || self.Address != actor.Address))
            {
                var radii = self.HitboxRadius + (actor?.HitboxRadius ?? 0.5f);
                var gap = HorizontalDistance(self.Position, unitPos) - radii;
                inRange = gap <= RangeFadeDistance;
                var t = Math.Clamp((gap - RangeFadeDistance) / RangeFadeWidth, 0f, 1f);
                alpha *= 1f - t * (1f - OutOfRangeAlpha);
            }
        }

        // Distance-fade (and any future external dimmer) multiplies the computed alpha.
        alpha *= alphaMultiplier;

        // Vertical layout, top to bottom. Text that sits above the bar (level, and the name unless it's
        // placed beside the icon) reserves a header row. The job icon and an overlapping mana bar are
        // positioned relative to the health bar.
        var fs = cfg.FontSize;
        var headerH = HeaderHeight(cfg);

        var hpH = cfg.ShowHealthBar ? cfg.HealthBarHeight : 0f;
        var mpH = snap.ShowMana ? cfg.ManaBarHeight : 0f;
        var castH = snap.Casting ? cfg.CastBarHeight : 0f;

        var y = headerH;
        var hpY = y;
        if (hpH > 0f) y += hpH;

        // Mana either stacks below the health bar or overlaps its bottom-right (taking no extra height).
        var mpY = 0f;
        if (mpH > 0f && !cfg.ManaOverlapHealth)
        {
            y += cfg.Gap;
            mpY = y;
            y += mpH;
        }

        // Cast either stacks below or, like the mana bar, overlaps the health bar (taking no extra height).
        var castY = 0f;
        if (castH > 0f && !cfg.CastOverlapHealth)
        {
            y += cfg.Gap;
            castY = y;
            y += castH;
        }

        var totalH = y;

        var origin = positionOverride ?? cfg.Position;

        // Normally the supplied position is the frame's top-left. Nameplates instead anchor the bar-top
        // line (hpY) — i.e. the header/bar boundary, which is the name baseline for name-only plates — to
        // the supplied point, so a label floats above the head while a health bar sits just below it. The
        // header lift then scales with the name font instead of needing a per-layout constant.
        if (anchorBarLine) origin.Y -= hpY;

        // Name size, measured once (drives icon docking, window bounds, and title placement).
        var hasName = cfg.ShowName && snap.Name.Length > 0;
        var nameSize = hasName ? _labels.Measure(snap.Name, cfg.NameFontSize) : Vector2.Zero;
        var nameLeft = origin.X + cfg.Width / 2f - nameSize.X / 2f; // centered-name left edge

        // Job icon: straddle the bar's left edge, or dock to the left of a centered name.
        var iconShown = cfg.ShowJobIcon && (snap.HasJob || snap.IconOverride != 0);
        var iconLeftX = origin.X - cfg.JobIconSize / 2f + cfg.JobIconOffsetX;
        var iconCenterY = origin.Y + hpY + hpH * cfg.JobIconAnchorY;
        var iconTopLeft = new Vector2(iconLeftX, iconCenterY - cfg.JobIconSize / 2f);
        if (cfg.JobIconLeftOfName && hasName)
        {
            var nameVCenter = origin.Y + hpY - nameSize.Y / 2f; // centered name sits above the baseline
            iconTopLeft = new Vector2(nameLeft - S(LabelGap) - cfg.JobIconSize + cfg.JobIconOffsetX, nameVCenter - cfg.JobIconSize / 2f);
        }

        if (cfg.JobIconOffsetY != 0f) iconTopLeft.Y += S(cfg.JobIconOffsetY);

        // A name placed right of the icon is left-aligned to here.
        var iconRightX = iconLeftX + cfg.JobIconSize;
        var nameAnchorLeft = (iconShown ? iconRightX : origin.X) + cfg.NameRightOfIconGap;

        var margin = S(12f); // room for the glow-fill bloom to bleed past the bars without clipping
        var top = origin.Y;
        var bottom = origin.Y + totalH;
        var left = origin.X;
        var right = origin.X + cfg.Width;
        if (iconShown)
        {
            top = MathF.Min(top, iconTopLeft.Y);
            bottom = MathF.Max(bottom, iconTopLeft.Y + cfg.JobIconSize);
            left = MathF.Min(left, iconTopLeft.X);
            right = MathF.Max(right, iconTopLeft.X + cfg.JobIconSize);
        }

        if (cfg.NameRightOfIcon && hasName)
            right = MathF.Max(right, nameAnchorLeft + nameSize.X);

        // A centered name can be wider than the bar (e.g. name-only nameplates) — widen the window.
        if (cfg.NameCentered && hasName)
        {
            left = MathF.Min(left, nameLeft);
            right = MathF.Max(right, nameLeft + nameSize.X);

            // The name-highlight texture spills past the text on every side — give it room.
            if (cfg.NameBackground)
            {
                var bgCx = origin.X + cfg.Width / 2f;
                var bgHalfW = nameSize.X / 2f + S(NameBgPadX);
                var bgCenterY = origin.Y + hpY + cfg.NameOffsetY - nameSize.Y / 2f + S(NameBgOffsetY);
                var bgHalfH = nameSize.Y / 2f + S(NameBgPadY);
                left = MathF.Min(left, bgCx - bgHalfW);
                right = MathF.Max(right, bgCx + bgHalfW);
                top = MathF.Min(top, bgCenterY - bgHalfH);
                bottom = MathF.Max(bottom, bgCenterY + bgHalfH);
            }
        }

        // A title line above/below the name needs room and may be wider than the bar.
        if (cfg.ShowName && !string.IsNullOrEmpty(title))
        {
            var titleSize = cfg.NameFontSize - TitleFontDrop;
            var titleH = _labels.Scale(titleSize);
            var titleW = _labels.Measure(title, titleSize).X;
            var center = origin.X + cfg.Width / 2f;
            left = MathF.Min(left, center - titleW / 2f);
            right = MathF.Max(right, center + titleW / 2f);
            if (titleAbove)
                top = MathF.Min(top, origin.Y + hpY - nameSize.Y - S(TitleGap) - titleH);
            else
                bottom = MathF.Max(bottom, origin.Y + hpY + S(TitleGap) + titleH);
        }

        // Leader crown: a small badge over the frame's top-left corner. Drawn on the frame's own draw
        // list (below) so it always sits above the bars, rather than in a separate window whose z-order
        // against the bar window is fragile.
        var hasLeader = leader && leaderIconSize > 0f;
        var leaderTopLeft = origin + leaderIconOffset;
        if (hasLeader)
        {
            top = MathF.Min(top, leaderTopLeft.Y);
            left = MathF.Min(left, leaderTopLeft.X);
            right = MathF.Max(right, leaderTopLeft.X + leaderIconSize);
            bottom = MathF.Max(bottom, leaderTopLeft.Y + leaderIconSize);
        }

        // Marker badge (e.g. a FATE icon): a small icon centered just above the frame's content.
        var hasMarker = markerIcon != 0;
        var markerSize = _labels.Scale(cfg.NameFontSize * 1.3f);
        var markerCenter = new Vector2(origin.X + cfg.Width / 2f, top - S(2f) - markerSize / 2f);
        if (hasMarker)
        {
            top = MathF.Min(top, markerCenter.Y - markerSize / 2f);
            left = MathF.Min(left, markerCenter.X - markerSize / 2f);
            right = MathF.Max(right, markerCenter.X + markerSize / 2f);
        }

        var windowPos = new Vector2(left - margin, top - margin);
        var windowSize = new Vector2(right - left + margin * 2f, bottom - top + margin * 2f);

        // A selected frame (the member is the player's target) gets a white border.
        var borderOverride = selected ? Colors.White : 0u;

        // Clickable frames take input. The hit area is the content (bars/icon/name), excluding the
        // glow margin, so clicks land where the frame actually is.
        var interactive = actor != null && inRange && (onLeftClick != null || onRightClick != null || onHover != null);
        var hitMin = new Vector2(left, top);
        var hitMax = new Vector2(right, bottom);

        void DrawBody(ImDrawListPtr dl)
        {
            if (interactive && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(hitMin, hitMax))
            {
                onHover?.Invoke(actor!);
                if (onLeftClick != null && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) onLeftClick(actor!);
                else if (onRightClick != null && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) onRightClick(actor!);
            }

            // Health bar.
            if (hpH > 0f)
            {
                var pos = new Vector2(origin.X, origin.Y + hpY);
                var size = new Vector2(cfg.Width, hpH);
                var frac = snap.HpMax > 0 ? snap.HpCurrent / (float)snap.HpMax : 0f;
                _bars.Draw(dl, pos, size, cfg.BackgroundColor, frac, snap.Color, cfg.BorderColor, alpha: alpha, borderOverride: borderOverride);

                // Absorb shield: a glowy blue overlay starting at the HP fill and running right; any part
                // that overflows the bar wraps back over the health from the left edge.
                if (snap.ShieldPct > 0)
                {
                    var shieldFrac = snap.ShieldPct / 100f;
                    _bars.DrawSegment(dl, pos, size, Colors.ShieldBar, frac, MathF.Min(1f, frac + shieldFrac), alpha);
                    var overflow = frac + shieldFrac - 1f;
                    if (overflow > 0f)
                        _bars.DrawSegment(dl, pos, size, Colors.ShieldBar, 0f, MathF.Min(1f, overflow), alpha);
                }

                if (cfg.HealthText != HealthTextMode.None)
                {
                    var (hx, hAnchor) = cfg.HealthTextOnLeft
                        ? (S(cfg.HealthTextPadX), DrawAnchor.Left)
                        : (cfg.Width - S(4f), DrawAnchor.Right);
                    _labels.Draw(dl, HealthText(cfg.HealthText, snap.HpCurrent, snap.HpMax),
                        pos + new Vector2(hx, hpH / 2f + cfg.HealthTextOffsetY), fs, cfg.TextColor, hAnchor, alpha: alpha);
                }
            }

            // Mana bar: stacked below, or overlapping the health bar's bottom edge (centered on it,
            // padded in from the right).
            if (mpH > 0f)
            {
                var manaW = cfg.Width * cfg.ManaWidthFactor;
                var pos = cfg.ManaOverlapHealth
                    ? new Vector2(origin.X + cfg.Width - manaW - S(ManaOverlapPad), origin.Y + hpY + hpH - mpH / 2f)
                    : new Vector2(origin.X, origin.Y + mpY);
                var size = new Vector2(manaW, mpH);
                var frac = snap.MpMax > 0 ? snap.MpCurrent / (float)snap.MpMax : 0f;
                _bars.Draw(dl, pos, size, cfg.BackgroundColor, frac, cfg.ManaColor, cfg.BorderColor, alpha: alpha);
            }

            // Cast bar (only while casting): stacked full-width below, or a thin overlap on the health
            // bar's bottom-right (like the mana bar) — the latter drops the name/time text.
            if (castH > 0f)
            {
                var castW = cfg.CastOverlapHealth ? cfg.Width * cfg.CastWidthFactor : cfg.Width;
                var pos = cfg.CastOverlapHealth
                    ? new Vector2(origin.X + cfg.Width - castW - S(OverlapPad), origin.Y + hpY + hpH - castH / 2f)
                    : new Vector2(origin.X, origin.Y + castY);
                var size = new Vector2(castW, castH);
                var frac = snap.CastTotal > 0f ? snap.CastCurrent / snap.CastTotal : 0f;
                var color = snap.CastInterruptible ? cfg.CastInterruptibleColor : cfg.CastColor;
                _bars.Draw(dl, pos, size, cfg.BackgroundColor, frac, color, cfg.BorderColor, alpha: alpha);

                var castFs = fs * CastFontScale;
                if (!cfg.CastOverlapHealth && cfg.ShowCastName)
                {
                    var name = ActionName(snap.CastActionId);
                    if (name.Length > 0)
                        _labels.Draw(dl, name, pos + new Vector2(S(4f), castH / 2f), castFs, cfg.TextColor,
                            DrawAnchor.Left, alpha: alpha);
                }

                if (!cfg.CastOverlapHealth && cfg.ShowCastTime)
                {
                    var remaining = MathF.Max(0f, snap.CastTotal - snap.CastCurrent);
                    _labels.Draw(dl, remaining.ToString("0.0"), pos + new Vector2(cfg.Width - S(4f), castH / 2f),
                        castFs, cfg.TextColor, DrawAnchor.Right, alpha: alpha);
                }
            }

            // Level and name, drawn over the bars so an over-bar name isn't hidden. The name is placed
            // right of the icon, centered over the bar, or left-aligned after the level per config.
            var baseline = origin.Y + hpY;
            var textLeft = origin.X + S(LevelPadding);

            var showLevel = cfg.ShowLevel && snap.Level > 0;
            var levelWidth = showLevel
                ? DrawLevel(dl, snap.Level, textLeft, baseline, cfg.LevelFontSize, cfg.TextColor, alpha)
                : 0f;

            var nameColor = cfg.NameUseJobColor ? snap.Color : cfg.TextColor;
            if (cfg.ShowName && snap.Name.Length > 0)
            {
                if (cfg.NameRightOfIcon)
                    _labels.Draw(dl, snap.Name, new Vector2(nameAnchorLeft, iconCenterY + cfg.NameOffsetY),
                        cfg.NameFontSize, nameColor, DrawAnchor.Left, alpha: alpha);
                else if (cfg.NameCentered)
                {
                    var nameCx = origin.X + cfg.Width / 2f;
                    if (cfg.NameBackground)
                        DrawNameBackground(dl,
                            new Vector2(nameCx, baseline + cfg.NameOffsetY - nameSize.Y / 2f + S(NameBgOffsetY)),
                            nameSize.X + 2f * S(NameBgPadX), nameSize.Y + 2f * S(NameBgPadY), alpha);
                    _labels.Draw(dl, snap.Name, new Vector2(nameCx, baseline + cfg.NameOffsetY),
                        cfg.NameFontSize, nameColor, DrawAnchor.Bottom, alpha: alpha);
                }
                else
                {
                    // Left-aligned: either above the bar (bottom on the bar top) or centered on the bar top.
                    var nameX = textLeft + (showLevel ? levelWidth + S(LabelGap) : 0f);
                    var nameAnchor = cfg.NameOnBarLine ? DrawAnchor.Left : DrawAnchor.BottomLeft;
                    _labels.Draw(dl, snap.Name, new Vector2(nameX, baseline + cfg.NameOffsetY), cfg.NameFontSize, nameColor, nameAnchor, alpha: alpha);
                }
            }

            // Optional title line (pet/minion owner, or a player's title) above or below the name.
            if (cfg.ShowName && !string.IsNullOrEmpty(title))
            {
                var titleSize = cfg.NameFontSize - TitleFontDrop;
                var cx = origin.X + cfg.Width / 2f;
                if (titleAbove)
                    _labels.Draw(dl, title, new Vector2(cx, baseline - nameSize.Y - S(TitleGap)), titleSize, cfg.TitleColor,
                        DrawAnchor.Bottom, alpha: alpha);
                else
                    _labels.Draw(dl, title, new Vector2(cx, baseline + S(TitleGap)), titleSize, cfg.TitleColor,
                        DrawAnchor.Top, alpha: alpha);
            }

            // Job icon, drawn last so it sits on top of the left of the bars.
            if (iconShown)
            {
                var iconId = snap.IconOverride != 0 ? snap.IconOverride : JobIcons.Colored(snap.JobId);
                var wrap = _icons.Get(iconId).GetWrapOrEmpty();
                dl.AddImage(wrap.Handle, iconTopLeft, iconTopLeft + new Vector2(cfg.JobIconSize),
                    Vector2.Zero, Vector2.One, Colors.MultiplyAlpha(0xFFFFFFFFu, alpha));
            }

            // Marker badge, on top, centered above the frame.
            if (hasMarker)
            {
                var wrap = _icons.Get(markerIcon).GetWrapOrEmpty();
                var tl = markerCenter - new Vector2(markerSize / 2f);
                dl.AddImage(wrap.Handle, tl, tl + new Vector2(markerSize), Vector2.Zero, Vector2.One,
                    Colors.MultiplyAlpha(0xFFFFFFFFu, alpha));
            }

            // Leader crown, drawn last so it sits above the bars and job icon.
            if (hasLeader)
            {
                var wrap = _icons.Get(LeaderIconId).GetWrapOrEmpty();
                dl.AddImage(wrap.Handle, leaderTopLeft, leaderTopLeft + new Vector2(leaderIconSize),
                    Vector2.Zero, Vector2.One, Colors.MultiplyAlpha(0xFFFFFFFFu, alpha));
            }

            // Ready-check mark (check / cross), centered on the health bar during a ready check.
            if (readyCheckIcon != 0 && hpH > 0f)
            {
                var rcSize = hpH * 1.3f;
                var center = new Vector2(origin.X + cfg.Width / 2f, origin.Y + hpY + hpH / 2f);
                var tl = center - new Vector2(rcSize / 2f);
                var wrap = _icons.Get(readyCheckIcon).GetWrapOrEmpty();
                dl.AddImage(wrap.Handle, tl, tl + new Vector2(rcSize), Vector2.Zero, Vector2.One,
                    Colors.MultiplyAlpha(0xFFFFFFFFu, alpha));
            }
        }

        // Nameplates pass a shared draw list so they can be depth-sorted (closer plates drawn last, on
        // top); every other frame gets its own invisible window (which also carries click input).
        if (drawListOverride is { } shared)
            DrawBody(shared);
        else
            DrawHelper.DrawInWindow(id, windowPos, windowSize, interactive, DrawBody);

        // Status effects live in their own (independently positioned) windows. List modules draw these
        // in a second pass (drawStatuses: false here, then DrawStatuses) so they sit above every row.
        if (drawStatuses) DrawStatusesInternal(id, cfg, origin, battle, preview, alpha);
    }

    /// <summary>
    ///     Draw only this frame's status collections, using the alpha captured by the matching
    ///     <see cref="Draw" /> call. Used for the two-pass list rendering (bars first, then statuses).
    /// </summary>
    public void DrawStatuses(string id, UnitFrameConfig cfg, IGameObject? actor, Vector2? positionOverride = null, PreviewUnit? preview = null)
    {
        if (!_states.TryGetValue(id, out var state) || state.Alpha <= 0.001f) return;
        DrawStatusesInternal(id, cfg, positionOverride ?? cfg.Position, actor as IBattleChara, preview, state.Alpha);
    }

    private void DrawStatusesInternal(string id, UnitFrameConfig cfg, Vector2 origin, IBattleChara? battle, PreviewUnit? preview, float alpha)
    {
        if (preview != null)
        {
            if (cfg.CombineStatuses)
                _statuses.DrawCombinedPreview(id + "_statuses", cfg.Debuffs, origin, preview.DebuffIcons, preview.BuffIcons, alpha);
            else
            {
                _statuses.DrawPreview(id + "_buffs", cfg.Buffs, origin, preview.BuffIcons, alpha);
                _statuses.DrawPreview(id + "_debuffs", cfg.Debuffs, origin, preview.DebuffIcons, alpha);
            }
        }
        else if (battle != null)
        {
            if (cfg.CombineStatuses)
                _statuses.DrawCombined(id + "_statuses", cfg.Debuffs, cfg.Debuffs, cfg.Buffs, origin, battle, alpha);
            else
            {
                _statuses.Draw(id + "_buffs", cfg.Buffs, origin, battle, true, alpha);
                _statuses.Draw(id + "_debuffs", cfg.Debuffs, origin, battle, false, alpha);
            }
        }
    }

    /// <summary>
    ///     Draws "Lv. N" left-aligned and bottom-aligned to <paramref name="bottomY" />, with the "Lv. "
    ///     prefix at 3/4 the size of the number. Returns the total drawn width.
    /// </summary>
    private float DrawLevel(ImDrawListPtr drawList, int level, float leftX, float bottomY, float size, uint color, float alpha)
    {
        const string prefix = "Lv. ";
        const float prefixScale = 0.75f;
        var prefixSize = size * prefixScale;
        var number = level.ToString();

        // The smaller prefix has less descent than the number, so box-bottom-aligning makes it sit
        // low. Nudge it up to share the number's visible baseline.
        var prefixNudge = (size - prefixSize) * 0.3f;
        _labels.Draw(drawList, prefix, new Vector2(leftX, bottomY - prefixNudge), prefixSize, color,
            DrawAnchor.BottomLeft, alpha: alpha);
        var prefixWidth = _labels.Measure(prefix, prefixSize).X;

        _labels.Draw(drawList, number, new Vector2(leftX + prefixWidth, bottomY), size, color,
            DrawAnchor.BottomLeft, alpha: alpha);
        return prefixWidth + _labels.Measure(number, size).X;
    }

    /// <summary>Distance between two points in the horizontal (X/Z) plane, ignoring height.</summary>
    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static string HealthText(HealthTextMode mode, uint current, uint max)
    {
        var pct = max > 0 ? (int)MathF.Round(current / (float)max * 100f) : 0;
        return mode switch
        {
            HealthTextMode.Value => current.ToString(),
            HealthTextMode.Percent => $"{pct}%",
            HealthTextMode.ValueAndPercent => $"{current}  ({pct}%)",
            _ => string.Empty
        };
    }

    /// <summary>
    ///     Draw the name highlight as a horizontal 3-slice (fixed rounded caps, stretched middle) centered
    ///     on <paramref name="center" />, sized to fit a name of the given content box.
    /// </summary>
    private void DrawNameBackground(ImDrawListPtr dl, Vector2 center, float contentW, float contentH, float alpha)
    {
        _nameBgTexture ??= _textures.GetFromGame(NameBgTexturePath);
        var wrap = _nameBgTexture.GetWrapOrEmpty();
        if (wrap.Width <= 0 || wrap.Height <= 0) return;

        var tint = Colors.MultiplyAlpha(NameBgTint, alpha * NameBgOpacity);

        float texW = wrap.Width, texH = wrap.Height;
        var u0 = NameBgSrc.X / texW;
        var v0 = NameBgSrc.Y / texH;
        var u1 = (NameBgSrc.X + NameBgSrc.Z) / texW;
        var v1 = (NameBgSrc.Y + NameBgSrc.W) / texH;
        var capU = NameBgCap / texW;

        var bgW = MathF.Max(contentW, 2f * NameBgCap);
        var bgH = contentH;
        var tl = center - new Vector2(bgW, bgH) / 2f;

        // Caps scale with the bar height (the source region is NameBgSrc.W tall) so they stay proportional.
        var capDest = MathF.Min(NameBgCap * (bgH / NameBgSrc.W), bgW / 2f);
        float y0 = tl.Y, y1 = tl.Y + bgH;
        float xL0 = tl.X, xL1 = tl.X + capDest, xR1 = tl.X + bgW, xR0 = xR1 - capDest;

        dl.AddImage(wrap.Handle, new Vector2(xL0, y0), new Vector2(xL1, y1), new Vector2(u0, v0), new Vector2(u0 + capU, v1), tint);
        dl.AddImage(wrap.Handle, new Vector2(xL1, y0), new Vector2(xR0, y1), new Vector2(u0 + capU, v0), new Vector2(u1 - capU, v1), tint);
        dl.AddImage(wrap.Handle, new Vector2(xR0, y0), new Vector2(xR1, y1), new Vector2(u1 - capU, v0), new Vector2(u1, v1), tint);
    }

    private string ActionName(uint actionId)
    {
        if (actionId == 0) return string.Empty;
        try
        {
            _actionSheet ??= _data.GetExcelSheet<LuminaAction>();
            return _actionSheet?.GetRow(actionId).Name.ExtractText() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Per-frame fade state, keyed by the frame's window id.</summary>
    private sealed class FrameState
    {
        public float Alpha;
        public Snapshot? Last;
    }

    /// <summary>
    ///     A frozen copy of everything needed to draw a frame, captured while the actor is present so
    ///     the frame can keep rendering (and fade out) after the actor is gone.
    /// </summary>
    private readonly record struct Snapshot(
        string Name,
        int Level,
        uint JobId,
        bool HasJob,
        uint IconOverride,
        uint HpCurrent,
        uint HpMax,
        byte ShieldPct,
        bool ShowMana,
        uint MpCurrent,
        uint MpMax,
        uint Color,
        bool Casting,
        float CastCurrent,
        float CastTotal,
        uint CastActionId,
        bool CastInterruptible)
    {
        public static Snapshot Preview(UnitFrameConfig cfg, PreviewUnit p)
        {
            const uint hpMax = 100000;
            const uint mpMax = 10000;
            return new Snapshot(
                p.Name, p.Level, p.JobId, p.JobId != 0, 0,
                (uint)(p.HpFraction * hpMax), hpMax, 0,
                cfg.ShowManaBar && (!cfg.ManaHealersOnly || JobRoles.IsHealer(p.JobId)),
                (uint)(p.MpFraction * mpMax), mpMax,
                p.Color ?? UnitColors.Job(p.JobId),
                false, 0f, 0f, 0, false);
        }

        public static Snapshot From(UnitFrameConfig cfg, IGameObject actor, ICharacter? character, IBattleChara? battle)
        {
            var casting = cfg.ShowCastBar && battle is { IsCasting: true } && battle.TotalCastTime > 0f;
            return new Snapshot(
                actor.Name.TextValue,
                character?.Level ?? 0,
                character?.ClassJob.RowId ?? 0,
                character is { ClassJob.RowId: > 0 },
                0,
                character?.CurrentHp ?? 0,
                character?.MaxHp ?? 0,
                character?.ShieldPercentage ?? 0,
                cfg.ShowManaBar && character is { MaxMp: > 0 }
                    && (!cfg.ManaHealersOnly || JobRoles.IsHealer(character.ClassJob.RowId)),
                character?.CurrentMp ?? 0,
                character?.MaxMp ?? 0,
                UnitColors.ForActor(actor),
                casting,
                battle?.CurrentCastTime ?? 0f,
                battle?.TotalCastTime ?? 0f,
                battle?.CastActionId ?? 0,
                battle?.IsCastInterruptible ?? false);
        }
    }
}
