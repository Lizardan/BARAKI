# Ориентация зданий на арене

База в local space: **+Z → центр карты / центральная дорога**, **−Z → край карты**.

Размещение: MatchArenaGreyboxBuilder.CreateBuildingMarker через
BaseLayoutDefinition.GetLocalRotation(buildingId).

| Здание | Куда смотрит дверь |
|--------|-------------------|
| BUILDING_MAIN | +Z (центральная дорога) |
| BUILDING_BARRACKS_CENTER | +Z (выход крипов по центру) |
| BUILDING_BARRACKS_LEFT | −X (выход на левый фланг) |
| BUILDING_BARRACKS_RIGHT | +X (выход на правый фланг) |
| `BUILDING_TOWER_NW` / `SW` | −X (слева от main) |
| `BUILDING_TOWER_NE` / `SE` | +X (справа от main) |

TT-меши (TownHall / Barracks / Tower): дверь автора на **local +X**, не на +Z.
Компенсация: BaseLayoutDefinition.BuildingModelYawDegrees (−90°), та же идея, что
UnitGreyboxVisuals.AnimatedHumanModelYawDegrees у юнитов.

## Pick / клик-выбор

`MatchBuildingPickPresenter` вешает **BoxCollider** (trigger, слой `MatchPickable`) на
`MeshFilter` визуала: `GreyboxVisual/Bases/Player_{slot}/{buildingId}`.
Поиск визуала — `MatchArenaGreybox.FindBuildingVisual`.

AABB здания больше силуэта (особенно TownHall). Поэтому `MatchSelectionInput` после
физика-хита проверяет треугольники меша (`MatchPickMeshRaycast`): луч через пустой
угол Main не выбирает Main и доходит до барака позади.

Не использовать MeshCollider для pick: отложенный `Destroy` в Play Mode снимает
компонент в конце кадра после повторного Refresh — клики перестают попадать.

Fallback — axis box без margin, если визуала ещё нет (тест / сервер без мешей).
Engage-радиус боя по-прежнему из `MatchPickFootprint.GetBuildingDiameter`.

## Руины

Убийство здания юнитами и карой main — один путь: HP → 0 → `IsRuins` →
`MatchBuildingFxPresenter` (взрыв + burning FX) + `BuildingRuinsVisual.ApplyRuins`.
В BARAKI Studio Кара зданий крутит тот же визуал на бараке: collapse + горение 2 с, затем цикл
(`RestoreIntact`).

Визуал: скрыть `Model` (полное здание), показать `Foundation` — TT construction mesh
`*_0` (TownHall_0 / Barracks_0 / Tower_A_0), тот же каменный цоколь что у целого здания.
Префабы собирает `TtBuildingVisualSetup` (`BARAKI/Buildings/Rebuild TT Prefabs`):
Foundation стартует inactive. Procedural-цилиндр больше не используется.
Корневой объект не выключается — на нём остаются FX огня.
