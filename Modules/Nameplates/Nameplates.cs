using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using SelUI.Game;
using SelUI.Modules.UnitFrames;
using SelUI.Rendering;
using CSCompanion = FFXIVClientStructs.FFXIV.Client.Game.Character.Companion;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using StructsFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace SelUI.Modules.Nameplates;

/// <summary>
///     World nameplates. Each active native nameplate is classified by type and drawn as the per-type
///     baked unit frame, positioned from the game's own nameplate node. Occlusion: "Full" mode against
///     "Walls" (hidden when behind geometry). The player's target is always shown
///     (any type, ignoring occlusion), still styled by type.
/// </summary>
public sealed unsafe class Nameplates : IHudModule
{
    private const int NameplateCount = 50;
    private const float VerticalOffset = 0f; // constant screen-px nudge from the node anchor (negative = up)
    private const int WallsRaycastFlag = 0x4000; // 0x2000 would also include objects

    // Distance-based opacity / scale curves (yalms from the player). Baked tuning; the on/off is the
    // user option. Full effect within the "near" radius, ramping to the floor by the "far" radius.
    private const float FadeNear = 15f, FadeFar = 50f, FadeMinAlpha = 0.2f;
    private const float ScaleNear = 15f, ScaleFar = 60f, ScaleMin = 0.5f;

    // Non-targeted enemies in combat are dimmed so the targeted enemy stands out (baked, always on).
    private const float EnemyOffTargetAlpha = 0.5f;

    // Per-actor head-height estimate (world yalms above the base), derived from the node and smoothed so
    // it's stable despite the node's lag. The intrinsic height barely changes, so we can project it live.
    private const float HeadHeightEma = 0.1f; // smoothing toward the latest derived sample
    private const float HeadHeightMin = 0.2f, HeadHeightMax = 40f, HeadHeightDefault = 2f;

    private readonly ICondition _condition;
    private readonly NameplatesConfig _config;
    private readonly UnitFrame _frame;
    private readonly IGameGui _gameGui;
    private readonly IObjectTable _objects;
    private readonly RenderScale _scale;
    private readonly ITargetManager _targets;

    // Smoothed head height per actor, plus the set of actors seen this frame so stale entries are pruned
    // (these would otherwise leak as actors come and go, like the fade-state map nameplates deliberately avoid).
    private readonly Dictionary<ulong, float> _headHeights = new();
    private readonly HashSet<ulong> _seenHeights = new();
    private readonly List<ulong> _pruneScratch = new();
    private readonly List<PlateDraw> _plates = new(); // reused each frame; cleared at the top of Draw

    public Nameplates(NameplatesConfig config, IObjectTable objects, ITargetManager targets, IGameGui gameGui, ICondition condition, UnitFrame frame, RenderScale scale)
    {
        _config = config;
        _objects = objects;
        _targets = targets;
        _gameGui = gameGui;
        _condition = condition;
        _frame = frame;
        _scale = scale;
    }

    public string Name => "Nameplates";

    public ModuleConfig Config => _config;

    public void Dispose()
    {
    }

    public void Draw()
    {
        var uiModule = StructsFramework.Instance()->GetUIModule();
        if (uiModule == null) return;

        var ui3d = uiModule->GetUI3DModule();
        if (ui3d == null) return;

        var addon = (AddonNamePlate*)_gameGui.GetAddonByName("NamePlate", 1).Address;
        if (addon == null) return;

        var atk = uiModule->GetRaptureAtkModule();
        var collision = StructsFramework.Instance()->BGCollisionModule;
        var camera = Control.Instance()->CameraManager.Camera->CameraBase.SceneCamera;
        var cameraPos = new Vector3(camera.Object.Position.X, camera.Object.Position.Y, camera.Object.Position.Z);

        var player = _objects.LocalPlayer;
        var self = player?.GameObjectId ?? 0;
        var playerPos = player?.Position;
        var playerInCombat = player != null && ActorState.InCombat(player);
        var target = _targets.Target;
        var targetId = target?.GameObjectId ?? 0;
        var foundTarget = false;

        var territory = TerritoryInfo.Instance();
        var inSanctuary = territory != null && territory->InSanctuary;
        var currentFate = FateHelper.CurrentFateId();

        var count = ui3d->NamePlateObjectInfoCount;
        _plates.Clear();
        var plates = _plates;
        _seenHeights.Clear();
        for (var i = 0; i < count; i++)
        {
            var info = ui3d->NamePlateObjectInfoPointers[i].Value;
            if (info == null || info->NamePlateIndex >= NameplateCount) continue;

            var obj = info->GameObject;
            if (obj == null) continue;

            var go = _objects.CreateObjectReference((nint)obj);
            if (go == null || go.GameObjectId == self) continue;

            // Dead actors keep a native nameplate for a moment — hide it (no lingering plates).
            if (IsDead(go)) continue;

            var isTarget = go.GameObjectId == targetId;
            var type = NameplateClassifier.Classify(go);
            var cfg = ConfigFor(go, type, playerInCombat);
            // NPCs / pets / minions only show while they're the target; others respect the enable flag.
            // Exception: players stay visible in cities when that option is on.
            // FATE membership (enemies), and the game's own nameplate marker (quest / important NPCs).
            var fateId = type == NameplateType.Enemy ? FateHelper.FateId(go) : (ushort)0;
            var inMyFate = fateId != 0 && fateId == currentFate;
            var gameMarker = atk != null && type is NameplateType.Npc or NameplateType.Enemy
                ? atk->NamePlateInfoEntries[info->NamePlateIndex].Icon
                : 0u;
            var importantShow = _config.ShowImportantNpcs && gameMarker != 0;

            // NPCs / pets / minions only show while targeted. Exceptions: players in cities, and (when the
            // option is on) quest / important NPCs the game has marked.
            var cityShow = type == NameplateType.Player && _config.ShowPlayersInCities && inSanctuary;
            if (!isTarget && !cityShow && !importantShow && (!cfg.Enabled || IsTargetOnly(type))) continue;

            // Out in the world, an idle enemy (not the target, not in a fight with us) shows nothing — a
            // name-only plate with no purpose is clutter. Kept in duties, in our FATE, or when it's a
            // marked quest/important enemy.
            if (!isTarget && type == NameplateType.Enemy && !inMyFate && !importantShow && !InDuty()
                && !(playerInCombat && ActorState.InCombat(go)))
                continue;

            // The game's nameplate node gives a correct (model-bounds) position, but its 2D screen coords
            // refresh only on the game's UI tick — slower than render — so they trail the camera on any
            // motion (the lateral shift, plus vertical shift on camera-tilt / jumping / slopes). The game's
            // own plate avoids this by re-projecting in 3D every frame, so we do the same: project the actor
            // ourselves each frame. WorldToScreen needs a world head height, and GameObject.Height is
            // 0/garbage for many objects — so we derive the true height from the node (back-projecting its
            // screen point onto the vertical line through the actor) and smooth it per actor. The height is
            // intrinsic and ~constant, so the smoothed value is stable even though the node it came from
            // lags, and projecting it live is lag-free. Fall back to the node only if projection fails.
            var root = addon->NamePlateObjectArray[info->NamePlateIndex].RootComponentNode;
            if (root == null) continue;
            var nodeScreen = new Vector2(
                root->AtkResNode.X + root->AtkResNode.Width / 2f,
                root->AtkResNode.Y + root->AtkResNode.Height);

            var feet = go.Position;
            var headHeight = EstimateHeadHeight(go.GameObjectId, feet, nodeScreen, ref camera);
            var screen = nodeScreen;
            if (_gameGui.WorldToScreen(feet, out var feetScreen)) screen.X = feetScreen.X;
            if (_gameGui.WorldToScreen(feet + new Vector3(0f, headHeight, 0f), out var headScreen)) screen.Y = headScreen.Y;

            // The target is always shown and ignores occlusion.
            if (!isTarget)
            {
                var distance = Vector3.Distance(cameraPos, go.Position);
                if (IsOccluded(ref camera, collision, screen, distance)) continue;
            }

            // Player-to-actor distance, used both for the fade/scale curves and for depth-sorting.
            var dist = playerPos is { } pp
                ? Vector3.Distance(pp, go.Position)
                : 0f;

            var alphaMul = 1f;

            // Non-targeted enemies in combat are dimmed so the target reads clearly.
            if (!isTarget && type == NameplateType.Enemy && playerInCombat && ActorState.InCombat(go)) alphaMul = EnemyOffTargetAlpha;

            // Distance fade / scale: everything except the target dims and shrinks with distance.
            if (!isTarget && playerPos != null)
            {
                if (_config.FadeByDistance) alphaMul *= DistanceFactor(dist, FadeNear, FadeFar, FadeMinAlpha);
                if (_config.ScaleByDistance)
                {
                    var scale = DistanceFactor(dist, ScaleNear, ScaleFar, ScaleMin);
                    if (scale < 0.999f) cfg = Scaled(cfg, scale);
                }
            }

            var (title, titleAbove) = TitleFor(go, type, atk, info->NamePlateIndex);
            // FATE mobs use their FATE's icon; otherwise fall back to the game's nameplate marker (quest).
            var fateMarker = fateId != 0 ? FateHelper.FateIcon(fateId) : 0u;
            var markerIcon = fateMarker != 0 ? fateMarker : gameMarker;
            plates.Add(new PlateDraw(go, cfg, screen, title, titleAbove, alphaMul, dist, isTarget, markerIcon));
            if (isTarget) foundTarget = true;
        }

        // Drop head-height estimates for actors that no longer have a nameplate (don't leak the map).
        if (_headHeights.Count > _seenHeights.Count)
        {
            _pruneScratch.Clear();
            foreach (var key in _headHeights.Keys)
                if (!_seenHeights.Contains(key))
                    _pruneScratch.Add(key);
            foreach (var key in _pruneScratch) _headHeights.Remove(key);
        }

        // Target had no active native nameplate — draw it anyway via world->screen. Never the local
        // player (you target yourself) nor a dead target: those nameplates must not show.
        if (target != null && target.GameObjectId != self && !foundTarget && !IsDead(target))
        {
            var cs = (CSGameObject*)target.Address;
            if (cs != null)
            {
                var head = target.Position + new Vector3(0f, cs->Height * 2.2f, 0f);
                if (_gameGui.WorldToScreen(head, out var screen))
                {
                    var type = NameplateClassifier.Classify(target);
                    var (title, titleAbove) = TitleFor(target, type, atk, -1);
                    var dist = playerPos is { } pp
                        ? Vector3.Distance(pp, target.Position)
                        : 0f;
                    var markerIcon = type == NameplateType.Enemy ? FateHelper.MarkerFor(target) : 0u;
                    plates.Add(new PlateDraw(target, ConfigFor(target, type, playerInCombat), screen, title, titleAbove, 1f, dist, true, markerIcon));
                }
            }
        }

        // Draw farthest first so closer nameplates land on top; the target is always drawn last (on top
        // of all of them). All plates share the ImGui background draw list, so submission order = z-order.
        plates.Sort(static (a, b) =>
        {
            if (a.IsTarget != b.IsTarget) return a.IsTarget ? 1 : -1;
            return b.Distance.CompareTo(a.Distance);
        });

        var drawList = ImGui.GetBackgroundDrawList();
        foreach (var p in plates)
            DrawPlate(p.Go, p.Cfg, p.Screen, p.Title, p.TitleAbove, p.AlphaMul, drawList, p.MarkerIcon);
    }

    public bool DrawConfig()
    {
        var changed = false;
        changed |= Toggle("Party job icon in combat", () => _config.PartyJobIconInCombat, v => _config.PartyJobIconInCombat = v);
        changed |= Toggle("Always show players in cities", () => _config.ShowPlayersInCities, v => _config.ShowPlayersInCities = v);
        changed |= Toggle("Show quest / important NPCs", () => _config.ShowImportantNpcs, v => _config.ShowImportantNpcs = v);
        changed |= Toggle("Fade nameplates by distance", () => _config.FadeByDistance, v => _config.FadeByDistance = v);
        changed |= Toggle("Scale nameplates by distance", () => _config.ScaleByDistance, v => _config.ScaleByDistance = v);
        return changed;
    }

    private static bool Toggle(string label, Func<bool> get, Action<bool> set)
    {
        var v = get();
        if (!ImGui.Checkbox(label, ref v)) return false;
        set(v);
        return true;
    }

    private void DrawPlate(IGameObject go, UnitFrameConfig cfg, Vector2 screen, string? title, bool titleAbove, float alphaMul,
        ImDrawListPtr drawList, uint markerIcon)
    {
        // Center the plate on the actor using the *scaled* width (UnitFrame grows the bar by the UI scale).
        var origin = new Vector2(screen.X - cfg.Width * _scale.Value / 2f, screen.Y + VerticalOffset);
        _frame.Draw($"SelUI_NP{go.GameObjectId}", cfg, go, origin, fade: false, title: title, titleAbove: titleAbove,
            alphaMultiplier: alphaMul, drawListOverride: drawList, markerIcon: markerIcon, anchorBarLine: true);
    }

    /// <summary>A nameplate queued for drawing, carrying its player distance so the batch can be depth-sorted.</summary>
    private readonly record struct PlateDraw(
        IGameObject Go, UnitFrameConfig Cfg, Vector2 Screen, string? Title, bool TitleAbove, float AlphaMul, float Distance,
        bool IsTarget, uint MarkerIcon);

    /// <summary>Types that only appear when they're the player's target.</summary>
    private static bool IsTargetOnly(NameplateType type)
    {
        return type is NameplateType.Player or NameplateType.AllianceMember
            or NameplateType.Npc or NameplateType.Pet or NameplateType.Minion;
    }

    private static bool IsPlayerType(NameplateType type)
    {
        return type is NameplateType.Player or NameplateType.PartyMember
            or NameplateType.AllianceMember or NameplateType.Friend;
    }

    /// <summary>
    ///     Title line for a nameplate: a player's FFXIV title (above when it's a prefix, else below) or a
    ///     pet/minion's owner name (below).
    /// </summary>
    private (string? title, bool above) TitleFor(IGameObject go, NameplateType type, RaptureAtkModule* atk, int npIndex)
    {
        if (IsPlayerType(type))
        {
            if (atk == null || npIndex < 0) return (null, false);
            var info = atk->NamePlateInfoEntries[npIndex];
            var title = info.Title.ToString();
            return string.IsNullOrEmpty(title) ? (null, false) : (title, info.IsPrefixTitle);
        }

        if (type is NameplateType.Pet or NameplateType.Minion)
        {
            var ownerId = type == NameplateType.Pet ? go.OwnerId : ((CSCompanion*)go.Address)->CompanionOwnerId;
            if (ownerId is 0 or 0xE0000000) return (null, false);
            return (_objects.SearchByEntityId(ownerId)?.Name.TextValue, false);
        }

        return (null, false);
    }

    /// <summary>Pick the per-type layout, with enemy idle / in-combat / targeted states.</summary>
    private UnitFrameConfig ConfigFor(IGameObject go, NameplateType type, bool playerInCombat)
    {
        // Party members optionally swap to an enlarged centered job icon while in combat.
        if (type == NameplateType.PartyMember && _config.PartyJobIconInCombat && ActorState.InCombat(go))
            return NameplateLayouts.PartyCombatIcon;

        if (type != NameplateType.Enemy) return NameplateLayouts.For(type);

        // The enemy health bar only shows during a fight we're part of: both the enemy and the player
        // must be in combat (otherwise it's just a name-only plate).
        if (!playerInCombat || !ActorState.InCombat(go)) return NameplateLayouts.EnemyIdle;

        var isTarget = _targets.Target != null && _targets.Target.GameObjectId == go.GameObjectId;
        if (isTarget) return NameplateLayouts.EnemyTarget;

        // Non-target enemies in combat hide their name in the overworld (clutter), but keep it in duties.
        return InDuty() ? NameplateLayouts.EnemyCombatNamed : NameplateLayouts.EnemyCombat;
    }

    /// <summary>A character with a health pool that has hit zero — dead. (HP-less NPCs/objects aren't.)</summary>
    private static bool IsDead(IGameObject go)
    {
        return go is ICharacter { MaxHp: > 0, CurrentHp: 0 };
    }

    private bool InDuty()
    {
        return _condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95);
    }

    /// <summary>1 within <paramref name="near" />, ramping linearly to <paramref name="min" /> by <paramref name="far" />.</summary>
    private static float DistanceFactor(float dist, float near, float far, float min)
    {
        if (dist <= near) return 1f;
        if (dist >= far) return min;
        return 1f - (1f - min) * ((dist - near) / (far - near));
    }

    /// <summary>A distance-scaled clone of a baked layout (never mutates the shared original).</summary>
    private static UnitFrameConfig Scaled(UnitFrameConfig cfg, float s)
    {
        var c = cfg.Clone();
        c.Width *= s;
        c.HealthBarHeight *= s;
        c.ManaBarHeight *= s;
        c.CastBarHeight *= s;
        c.NameFontSize *= s;
        c.LevelFontSize *= s;
        c.FontSize *= s;
        c.JobIconSize *= s;
        c.JobIconOffsetX *= s;
        c.NameRightOfIconGap *= s;
        c.Gap *= s;
        c.Buffs.IconSize *= s;
        c.Buffs.FontSize *= s;
        c.Buffs.Position *= s;
        c.Debuffs.IconSize *= s;
        c.Debuffs.FontSize *= s;
        c.Debuffs.Position *= s;
        return c;
    }

    /// <summary>
    ///     Smoothed estimate of an actor's head height (world yalms above its base). Casts the camera ray
    ///     through the node's head screen point and intersects it with the vertical line through the
    ///     actor's base — the node's screen position is laggy but its *height* is correct, and height is
    ///     intrinsic, so an EMA gives a stable value we can then project live each frame without lag.
    /// </summary>
    private float EstimateHeadHeight(ulong id, Vector3 feet, Vector2 nodeScreen, ref Camera camera)
    {
        _seenHeights.Add(id);

        var ray = camera.ScreenPointToRay(nodeScreen);
        var origin = new Vector3(ray.Origin.X, ray.Origin.Y, ray.Origin.Z);
        var dir = new Vector3(ray.Direction.X, ray.Direction.Y, ray.Direction.Z);

        // Closest point on the ray to the actor's vertical axis, solved in the horizontal plane.
        var measured = float.NaN;
        var denom = dir.X * dir.X + dir.Z * dir.Z;
        if (denom > 1e-6f)
        {
            var t = ((feet.X - origin.X) * dir.X + (feet.Z - origin.Z) * dir.Z) / denom;
            if (t > 0f)
            {
                var h = origin.Y + t * dir.Y - feet.Y;
                if (h is > HeadHeightMin and < HeadHeightMax) measured = h;
            }
        }

        if (!_headHeights.TryGetValue(id, out var current))
            current = float.IsNaN(measured) ? HeadHeightDefault : measured;
        else if (!float.IsNaN(measured))
            current += (measured - current) * HeadHeightEma;

        _headHeights[id] = current;
        return current;
    }

    // "Full" occlusion against "Walls": two rays (±30px) from the camera through the nameplate point;
    // occluded only if both are blocked.
    private bool IsOccluded(ref Camera camera, BGCollisionModule* collision, Vector2 screen, float distance)
    {
        if (collision == null) return false;

        var flag = WallsRaycastFlag;
        var flags = stackalloc int[] { flag, 0, flag, 0 };
        Span<Vector2> points = [screen + new Vector2(-30f, 0f), screen + new Vector2(30f, 0f)];

        var blocked = 0;
        foreach (var point in points)
        {
            var ray = camera.ScreenPointToRay(point);
            var origin = new Vector3(ray.Origin.X, ray.Origin.Y, ray.Origin.Z);
            var direction = new Vector3(ray.Direction.X, ray.Direction.Y, ray.Direction.Z);

            RaycastHit hit;
            if (collision->RaycastMaterialFilter(&hit, &origin, &direction, distance, 1, flags))
                blocked++;
        }

        return blocked == points.Length;
    }
}
