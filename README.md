# SelUI

A streamlined, deliberately FF-flavored HUD replacement for FFXIV (Dalamud plugin).

Where DelvUI gives you endless knobs, SelUI gives you a specific, opinionated look with minimal
configuration. Features are **plug-and-play modules** you turn on and off — unit frames, party
frames, nameplates, and so on.

## Status

Early foundation. Implemented so far:

- **Rendering primitives**
  - `FontManager` — bundled Miedinger default font, fully customizable per PrettyFly's approach.
  - `LabelRenderer` — anchored, outlined text drawing.
  - `BarRenderer` — composited bars (background → fill layers → border), any fill direction.
  - `DrawHelper` — invisible per-element overlay windows + anchor math.
- **Module framework** — `IHudModule` + `HudManager`, each module independently toggleable with its
  own settings, driven from one config window. Add a module to the list in `Plugin.cs` and it shows
  up everywhere automatically.
- **Player unit frame** — HP/MP bars, name / level / job, current values. The reference module for
  the unit-frame family.

## Usage

`/selui` opens the settings window (also available from the plugin installer cog).

## Building

Requires the Dalamud SDK. Built against the shared Dalamud refs at `../dalamud/` (sibling directory),
matching the other plugins in this workspace. Build on Windows with the .NET 10 SDK:

```
dotnet build -c Release
```

## Layout

```
Rendering/   FontManager, Colors, DrawHelper, LabelRenderer, BarRenderer
Modules/     IHudModule, ModuleConfig, HudManager
  UnitFrames/  PlayerUnitFrame (+ config)
Configuration/  root Configuration
UI/          ConfigWindow
```
