# Fog of War

Local-client presentation only (no networking).

## Components

| Type | Role |
|------|------|
| `MatchFogOfWar` | Owns permanent zones + vision radius; disables fog when local player is eliminated or `DivineBlessingComplete` |
| `FogSimulation` | Density compute (`Resources/Fog/FogSim`) + world fullscreen volume + **minimap RGBA overlay** |
| `FogVisionRules` | Pure reveal / select / minimap-blip rules (Edit Mode tests) |
| `MatchMinimapFogElement` | UI Toolkit textured quad on the minimap (same density field, yaw-aware UVs) |

## Divine Blessing

`UPG_MAIN_DIVINE_BLESSING` → `MatchPlayerState.DivineBlessingComplete` → `MatchFogOfWar.FogDisabled`.
Hides **world volume** and **minimap overlay**; enemy blips become visible via existing `IsRevealed` / `ShouldShowUnitOnMinimap`.

## Minimap

`MatchMinimapController` samples `MatchFogOfWar.TryGetMinimapOverlay` each `LateUpdate`.
Overlay UV space = world XZ / `FogSimulation.AreaSize` (same as volume shader).
