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
    private readonly Configuration.Configuration _config;
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
        IReadOnlyList<IHudModule> modules)
        : base("SelUI Settings###SelUIConfig")
    {
        _config = config;
        _pluginInterface = pluginInterface;
        _uiBuilder = (UiBuilder)pluginInterface.UiBuilder;
        _fontManager = fontManager;
        _labels = labels;
        _modules = modules;

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
                changed |= module.DrawConfig();
            }
        }

        if (changed) _config.Save(_pluginInterface);
    }

    private bool DrawFontPicker()
    {
        ImGui.TextUnformatted("Font:");
        ImGui.SameLine();
        ImGui.TextDisabled(_config.Font != null ? FontFamilyName(_config.Font) : "Miedinger (Default)");
        ImGui.SameLine();

        if (ImGui.SmallButton("Choose...##font"))
        {
            _fontChooser = SingleFontChooserDialog.CreateAuto(_uiBuilder);
            if (_config.Font != null) _fontChooser.SelectedFont = _config.Font;
        }

        if (_config.Font == null) return false;

        ImGui.SameLine();
        if (!ImGui.SmallButton("Reset##font")) return false;

        _fontManager.ReleaseHandle(_config.Font);
        _config.Font = null;
        _labels.GlobalFont = null;
        return true;
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
