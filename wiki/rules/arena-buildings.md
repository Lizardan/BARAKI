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
| башни | +Z (как main) |

TT-меши (TownHall / Barracks / Tower): дверь автора на **local +X**, не на +Z.
Компенсация: BaseLayoutDefinition.BuildingModelYawDegrees (−90°), та же идея, что
UnitGreyboxVisuals.AnimatedHumanModelYawDegrees у юнитов.

## Pick / клик-выбор

`MatchBuildingPickPresenter` вешает **MeshCollider** на `MeshFilter` визуала здания
(`GreyboxVisual/Player_{slot}/{buildingId}`), слой `MatchPickable`.

Почему не AABB-прокси: у TownHall и др. силуэт не заполняет world AABB; с камеры RTS
луч в барак / башню часто первым пересекает «пустой» объём Main и выбирает не тот объект.
Mesh-коллайдер совпадает с видимой геометрией. Fallback — axis box без margin, если
визуала ещё нет (тест / сервер без мешей).

Engage-радиус боя по-прежнему из `MatchPickFootprint.GetBuildingDiameter` (не из pick-меша).
