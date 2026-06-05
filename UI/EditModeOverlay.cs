using Dalamud.Bindings.ImGui;
using SelUI.Modules;
using SelUI.Rendering;

namespace SelUI.UI;

/// <summary>
///     Draws HUD layout edit mode: a labeled, draggable outline box over each movable module. Dragging
///     a box shifts that module's stored position; the new position is saved when the drag is released.
///     Subscribe its <see cref="Draw" /> to <c>UiBuilder.Draw</c> after the HUD so the boxes sit on top
///     of (and capture input ahead of) the live frames.
/// </summary>
public sealed class EditModeOverlay
{
    private static readonly uint FillColor = Colors.Rgba(0x33, 0x99, 0xFF, 0x33);
    private static readonly uint BorderColor = Colors.Rgba(0x66, 0xCC, 0xFF, 0xFF);

    private readonly LabelRenderer _labels;
    private readonly IReadOnlyList<IMovableModule> _modules;
    private readonly Action _save;
    private readonly EditModeState _state;

    public EditModeOverlay(IReadOnlyList<IMovableModule> modules, LabelRenderer labels, EditModeState state, Action save)
    {
        _modules = modules;
        _labels = labels;
        _state = state;
        _save = save;
    }

    public void Draw()
    {
        if (!_state.Active) return;

        for (var i = 0; i < _modules.Count; i++)
        {
            var module = _modules[i];
            var topLeft = module.EditTopLeft;
            var size = module.EditSize;
            if (size.X <= 0f || size.Y <= 0f) continue;

            var id = $"SelUI_Edit{i}";
            DrawHelper.DrawInWindow(id, topLeft, size, true, dl =>
            {
                ImGui.SetCursorPos(Vector2.Zero);
                ImGui.InvisibleButton(id, size);

                if (ImGui.IsItemActive())
                {
                    var delta = ImGui.GetIO().MouseDelta;
                    if (delta.X != 0f || delta.Y != 0f) module.MoveBy(delta);
                }

                if (ImGui.IsItemDeactivated()) _save();

                var min = topLeft;
                var max = topLeft + size;
                dl.AddRectFilled(min, max, FillColor);
                dl.AddRect(min, max, BorderColor, 0f, ImDrawFlags.None, 2f);

                _labels.Draw(dl, module.EditLabel, min + size / 2f, 16f, Colors.White);
            });
        }
    }
}
