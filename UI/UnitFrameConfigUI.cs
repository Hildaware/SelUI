using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using SelUI.Modules.UnitFrames;
using SelUI.Rendering;

namespace SelUI.UI;

/// <summary>Shared settings editor for any unit frame. Used by every <see cref="UnitFrameModule" />.</summary>
public static class UnitFrameConfigUI
{
    private static readonly string[] HealthTextItems = ["None", "Value", "Percent", "Value + Percent"];

    public static bool Draw(UnitFrameConfig cfg)
    {
        var changed = false;

        changed |= Vec2("Position", () => cfg.Position, v => cfg.Position = v);
        changed |= Drag("Width", () => cfg.Width, v => cfg.Width = v, 1f, 60f, 800f);
        changed |= Drag("Font size", () => cfg.FontSize, v => cfg.FontSize = v, 0.5f, 8f, 48f);
        changed |= Check("Hide when no actor", () => cfg.HideWhenNoActor, v => cfg.HideWhenNoActor = v);

        Section("Health");
        changed |= Check("Health bar", () => cfg.ShowHealthBar, v => cfg.ShowHealthBar = v);
        changed |= Drag("Health height", () => cfg.HealthBarHeight, v => cfg.HealthBarHeight = v, 0.5f, 4f, 80f);
        changed |= Combo("Health text", () => (int)cfg.HealthText, v => cfg.HealthText = (HealthTextMode)v, HealthTextItems);

        Section("Mana");
        changed |= Check("Mana bar", () => cfg.ShowManaBar, v => cfg.ShowManaBar = v);
        changed |= Drag("Mana height", () => cfg.ManaBarHeight, v => cfg.ManaBarHeight = v, 0.5f, 4f, 80f);
        changed |= Drag("Mana width factor", () => cfg.ManaWidthFactor, v => cfg.ManaWidthFactor = v, 0.01f, 0.1f, 1f, "%.2f");
        changed |= Check("Mana overlaps health", () => cfg.ManaOverlapHealth, v => cfg.ManaOverlapHealth = v);
        changed |= Color("Mana color", () => cfg.ManaColor, v => cfg.ManaColor = v);

        Section("Cast");
        changed |= Check("Cast bar", () => cfg.ShowCastBar, v => cfg.ShowCastBar = v);
        changed |= Drag("Cast height", () => cfg.CastBarHeight, v => cfg.CastBarHeight = v, 0.5f, 4f, 80f);
        changed |= Check("Cast name", () => cfg.ShowCastName, v => cfg.ShowCastName = v);
        changed |= Check("Cast time", () => cfg.ShowCastTime, v => cfg.ShowCastTime = v);
        changed |= Color("Cast color", () => cfg.CastColor, v => cfg.CastColor = v);
        changed |= Color("Interruptible color", () => cfg.CastInterruptibleColor, v => cfg.CastInterruptibleColor = v);

        Section("Name / Job");
        changed |= Check("Name", () => cfg.ShowName, v => cfg.ShowName = v);
        changed |= Check("Level", () => cfg.ShowLevel, v => cfg.ShowLevel = v);
        changed |= Check("Center name", () => cfg.NameCentered, v => cfg.NameCentered = v);
        changed |= Check("Name right of icon", () => cfg.NameRightOfIcon, v => cfg.NameRightOfIcon = v);
        changed |= Drag("Name-icon gap", () => cfg.NameRightOfIconGap, v => cfg.NameRightOfIconGap = v, 0.5f, -40f, 40f);
        changed |= Drag("Name font size", () => cfg.NameFontSize, v => cfg.NameFontSize = v, 0.5f, 8f, 48f);
        changed |= Drag("Level font size", () => cfg.LevelFontSize, v => cfg.LevelFontSize = v, 0.5f, 8f, 48f);
        changed |= Check("Job icon", () => cfg.ShowJobIcon, v => cfg.ShowJobIcon = v);
        changed |= Drag("Job icon size", () => cfg.JobIconSize, v => cfg.JobIconSize = v, 0.5f, 8f, 96f);
        changed |= Drag("Job icon X offset", () => cfg.JobIconOffsetX, v => cfg.JobIconOffsetX = v, 0.5f, -40f, 40f);
        changed |= Drag("Job icon vertical anchor", () => cfg.JobIconAnchorY, v => cfg.JobIconAnchorY = v, 0.01f, 0f, 1f, "%.2f");

        Section("Appearance");
        changed |= Color("Background", () => cfg.BackgroundColor, v => cfg.BackgroundColor = v);
        changed |= Color("Border", () => cfg.BorderColor, v => cfg.BorderColor = v);
        changed |= Color("Text", () => cfg.TextColor, v => cfg.TextColor = v);

        if (ImGui.CollapsingHeader("Buffs")) changed |= DrawStatusList("Buffs", cfg.Buffs);
        if (ImGui.CollapsingHeader("Debuffs")) changed |= DrawStatusList("Debuffs", cfg.Debuffs);

        return changed;
    }

    private static bool DrawStatusList(string key, StatusListConfig s)
    {
        using var indent = ImRaii.PushIndent();
        using var id = ImRaii.PushId(key);

        var changed = false;
        changed |= Check("Enabled", () => s.Enabled, v => s.Enabled = v);
        changed |= Check("Only mine", () => s.OwnOnly, v => s.OwnOnly = v);
        changed |= Check("Crop icon", () => s.CropIcon, v => s.CropIcon = v);
        changed |= Vec2("Offset", () => s.Position, v => s.Position = v);
        changed |= Drag("Icon size", () => s.IconSize, v => s.IconSize = v, 0.5f, 8f, 64f);
        changed |= Drag("Max icons", () => s.MaxIcons, v => s.MaxIcons = (int)v, 1f, 1f, 40f);
        changed |= Drag("Per line", () => s.PerLine, v => s.PerLine = (int)v, 1f, 1f, 20f);
        changed |= Check("Grow right", () => s.GrowRight, v => s.GrowRight = v);
        changed |= Check("Grow down", () => s.GrowDown, v => s.GrowDown = v);
        changed |= Check("Show duration", () => s.ShowDuration, v => s.ShowDuration = v);
        changed |= Check("Show stacks", () => s.ShowStacks, v => s.ShowStacks = v);
        changed |= Drag("Font size", () => s.FontSize, v => s.FontSize = v, 0.5f, 6f, 32f);
        return changed;
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(title);
    }

    private static bool Check(string label, Func<bool> get, Action<bool> set)
    {
        var v = get();
        if (!ImGui.Checkbox(label, ref v)) return false;
        set(v);
        return true;
    }

    private static bool Drag(string label, Func<float> get, Action<float> set, float speed, float min, float max, string fmt = "%.0f")
    {
        var v = get();
        ImGui.SetNextItemWidth(160f);
        if (!ImGui.DragFloat(label, ref v, speed, min, max, fmt)) return false;
        set(v);
        return true;
    }

    private static bool Vec2(string label, Func<Vector2> get, Action<Vector2> set)
    {
        var v = get();
        ImGui.SetNextItemWidth(220f);
        if (!ImGui.DragFloat2(label, ref v)) return false;
        set(v);
        return true;
    }

    private static bool Combo(string label, Func<int> get, Action<int> set, string[] items)
    {
        var v = get();
        ImGui.SetNextItemWidth(160f);
        if (!ImGui.Combo(label, ref v, items, items.Length)) return false;
        set(v);
        return true;
    }

    private static bool Color(string label, Func<uint> get, Action<uint> set)
    {
        var v = Colors.ToVector4(get());
        if (!ImGui.ColorEdit4(label, ref v, ImGuiColorEditFlags.AlphaBar)) return false;
        set(Colors.FromVector4(v));
        return true;
    }
}
