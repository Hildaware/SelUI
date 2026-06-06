using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ImGuiFontChooserDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using SelUI.Modules;
using SelUI.Rendering;

namespace SelUI.UI;

/// <summary>
///     SelUI's only configuration window: a master switch, the global font picker, and a collapsing
///     section per module with an enable toggle and that module's own settings. Settings save the moment
///     they change.
/// </summary>
public sealed class ConfigWindow : Window
{
    private const float FontScaleMin = 0.75f;
    private const float FontScaleMax = 1.5f;

    private readonly Configuration.Configuration _config;
    private readonly EditModeState _editState;
    private readonly FontManager _fontManager;
    private readonly LabelRenderer _labels;
    private readonly IReadOnlyList<IHudModule> _modules;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly UiBuilder _uiBuilder;

    private SingleFontChooserDialog? _fontChooser;

    public ConfigWindow(
        Configuration.Configuration config,
        IDalamudPluginInterface pluginInterface,
        FontManager fontManager,
        LabelRenderer labels,
        IReadOnlyList<IHudModule> modules,
        EditModeState editState)
        : base("SelUI Settings###SelUIConfig")
    {
        _config = config;
        _pluginInterface = pluginInterface;
        _uiBuilder = (UiBuilder)pluginInterface.UiBuilder;
        _fontManager = fontManager;
        _labels = labels;
        _modules = modules;
        _editState = editState;

        Size = new Vector2(420f, 520f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var changed = ResolvePendingFont();

        var enabled = _config.Enabled;
        if (ImGui.Checkbox("Enable SelUI", ref enabled))
        {
            _config.Enabled = enabled;
            changed = true;
        }

        ImGui.Separator();
        changed |= DrawFontPicker();
        ImGui.Separator();

        if (ImGui.Button(_editState.Active ? "Lock HUD Layout" : "Edit HUD Layout"))
            _editState.Active = !_editState.Active;
        if (_editState.Active)
            ImGui.TextDisabled("Drag a frame to move it; changes save automatically.");

        ImGui.Separator();

        ImGui.TextDisabled("Modules");
        ImGui.Spacing();

        foreach (var module in _modules)
        {
            var moduleEnabled = module.Config.Enabled;
            if (ImGui.Checkbox($"##enable_{module.Name}", ref moduleEnabled))
            {
                module.Config.Enabled = moduleEnabled;
                changed = true;
            }

            ImGui.SameLine();
            if (ImGui.CollapsingHeader(module.Name))
            {
                using var indent = ImRaii.PushIndent();
                using var disabled = ImRaii.Disabled(!module.Config.Enabled);
                // Scope each module's widget IDs by name so shared labels ("Position", "Show preview")
                // across modules don't collide into one ImGui id (which makes later ones uninteractable).
                using var id = ImRaii.PushId(module.Name);
                changed |= module.DrawConfig();
            }
        }

        if (changed) _config.Save(_pluginInterface);
    }

    private bool DrawFontPicker()
    {
        var changed = false;
        ImGui.TextUnformatted("Font");

        // Bundled fonts (the .ttf files shipped in Media/Fonts). A picked system font overrides this.
        var current = _config.BundledFont ?? FontManager.DefaultBundledFont;
        var preview = _config.Font != null ? FontFamilyName(_config.Font) : PrettyFontName(current);
        ImGui.SetNextItemWidth(220f);
        using (var combo = ImRaii.Combo("##bundledfont", preview))
        {
            if (combo)
                foreach (var name in _fontManager.BundledFontNames)
                    if (ImGui.Selectable(PrettyFontName(name), _config.Font == null && name == current))
                    {
                        _config.BundledFont = name == FontManager.DefaultBundledFont ? null : name;
                        _fontManager.ActiveBundledFont = name;
                        _fontManager.ReleaseHandle(_config.Font);
                        _config.Font = null;
                        _labels.GlobalFont = null;
                        changed = true;
                    }
        }

        // System font chooser. When set, the chosen system font takes priority over the bundled one.
        if (ImGui.SmallButton("Choose System Font...##font"))
        {
            _fontChooser = SingleFontChooserDialog.CreateAuto(_uiBuilder);
            if (_config.Font != null) _fontChooser.SelectedFont = _config.Font;
        }

        if (_config.Font != null)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear System Font##font"))
            {
                _fontManager.ReleaseHandle(_config.Font);
                _config.Font = null;
                _labels.GlobalFont = null;
                changed = true;
            }
        }

        // One global multiplier for all text, in place of per-element size knobs.
        var scale = _config.FontScale;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Font Scale##fontscale", ref scale, FontScaleMin, FontScaleMax, "%.2fx"))
        {
            scale = Math.Clamp(scale, FontScaleMin, FontScaleMax);
            _config.FontScale = scale;
            _labels.GlobalScale = scale;
            changed = true;
        }

        return changed;
    }

    /// <summary>"SpaceGrotesk" → "Space Grotesk"; the default is tagged so users know it's the fallback.</summary>
    private static string PrettyFontName(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(name[i]);
        }

        if (name == FontManager.DefaultBundledFont) sb.Append(" (Default)");
        return sb.ToString();
    }

    private bool ResolvePendingFont()
    {
        if (_fontChooser is not { ResultTask.IsCompleted: true }) return false;

        var changed = false;
        if (!_fontChooser.ResultTask.IsCanceled && !_fontChooser.ResultTask.IsFaulted)
        {
            _fontManager.ReleaseHandle(_config.Font);
            _config.Font = _fontChooser.ResultTask.Result;
            _labels.GlobalFont = _config.Font;
            changed = true;
        }

        _fontChooser = null;
        return changed;
    }

    private static string FontFamilyName(SingleFontSpec spec)
    {
        var s = spec.FontId.ToString() ?? string.Empty;
        var i = s.LastIndexOf(':');
        return i >= 0 ? s[(i + 1)..] : s;
    }
}
