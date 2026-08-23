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

## Presentation cull (simulation unchanged)

FoW hides **presentation only**. Combat, projectiles, and snapshots keep running in fog.

- One-shot VFX (ability, blood, impact, catapult splash, building FX): spawn only if
  `FogVisionRules.CanSpawnPresentationFx` (`FogDisabled` or the world point is revealed).
  No point / fog off → spawn as before.
- Projectile mesh: do not create or update while the current position is hidden; when it
  exits fog, show mid-flight from the spawn clock. Re-entering fog hides/recycles the mesh.
- Hidden enemy units: `Animator.enabled = false`, no aura loops, no `DriveAnimator`.
  Local-player units stay fully presented. `FogDisabled` (Divine Blessing / spectator) skips culls.
