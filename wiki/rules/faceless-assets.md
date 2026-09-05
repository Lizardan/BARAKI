# Faceless (Древние) — review-ассеты

Раса в UI — **Древние**. Папки и id — **Faceless** (`RACE_FACELESS`). Пока раса не
зафиналена, work items в HacknPlan — Urgent («Горит»).

## Пока нет маппинга на роли

Не создавать дерево `Units/BonusUnits/Heroes` как у людей. Сначала просмотр
пронумерованных моделей.

```text
Art/Races/Faceless/          # OBJ + PNG из WC3 MDX/BLP
Art/Races/Faceless/Meshes/   # skinned Mesh из MDX (review)
Art/Races/Faceless/Anim/     # Stand/Walk/Attack .anim + .controller
Prefabs/Races/Faceless/_Review/01_…12_….prefab
Scenes/Dev/FacelessReview.unity   # не в Build Settings
```

Конвертер статики: `Tooling/MdxReview/convert_mdx_to_obj.py` (MDX v800).
Скин + клипы: меню `BARAKI/Faceless/Rebuild Review Anims` (`FacelessReviewAnimBuilder`).
На `FacelessReview` в Play — кнопки Idle / Бег / Удар (клавиши 1/2/3) через
`Animator.CrossFade` (как боевые юниты), без AnyState. Team color на просмотре —
слот 1 (синий).

MDX держат **несколько слоёв** на материал (WC3 `LAYS`): снизу replaceable
**team color** (цвет слота игрока), сверху диффуз с альфой. Где альфа диффуза = 0,
просвечивает цвет игрока — это не вырезы. Старый `clip(luma)` дырявил тёмную
броню и кожу. Review-шейдер `Game/Faceless/ReviewUnlit`: `lerp(albedo, _TeamColor, 1-a)`,
без luma-клипа. FilterMode Transparent (1) без team color — **alpha clip**, но не
для скина `FacelessOneUnbrokenV2`: JPEG-альфа дырявит тело (`08_FacelessKing`).
FilterMode Blend (2) без team color — полупрозрачность. Cutout — чужие WC3-текстуры
(крылья, волосы), не V2.

Геосеты **только из replaceable team color** (без диффуза) — **сохраняются** как
чисrый team color: стем `TeamColor` (белая текстура + `_BaseColor` слота),
`twoSided=true`. Это не подложка под FX, а настоящая геометрия — середина рог
`08_FacelessKing` (12 tris). Единственный такой геосет во всех 12 моделях; FX-подложки
(без диффуза и без team) по-прежнему отсекаются по `IsFxTexture`/JUNK.
`HeroAvatarFlame` на короле — **не FX**, а kitbash (броня / лава / рога с атласа);
без этих геосетов в теле дыры. Альфу JPEG не клипать (как V2). Пустой Magos-фон
атласа `(130,147,178)` почти как небо review-сцены — `_AtlasClip` перекрашивает
его в тёмную бирюзу (не `clip`: иначе дыры).

**Прозрачные треугольники V2-скина** (`FacelessOneUnbrokenV2`, крупные геосеты,
не team/blend/clip) расщепляются при сборке в два submesh'а: непрозрачная часть
+ clip-часть (`twoSided=true`, `ClipBlack=true`) по альфе центроида UV < 0.45
(`SplitV2TransparentTriangles` в `FacelessReviewAnimBuilder`). Так JPEG-дыры
(наплечники `01`, рога/шипы `08`, лицо `11`) становятся вырезом вместо чёрных
пятен, тело остаётся целым. Мелкие V2-геосеты FilterMode 1
(<80 вершин: грива/цепи/шипы) — cutout на уровне геосета, иначе чёрные карточки атласа.
Текстуры WC3/V2 — `Wrap Repeat`. `rootBone` — `Bone_Root`, не `gutz`.

**Наконечник копья короля**: `ForgottenOne.png` регион U[0.74,1.0] × V[0,0.29]
перекрашен в стальной серый (luma-нейтральный, idempotent) — меню
`BARAKI/Faceless/Recolor ForgottenOne Tip`; вызывается и в `Rebuild Review Anims`
до `AssetDatabase.Refresh`. `ForgottenOne` использует только `FacelessKing`.

Цвета: `05–07` колдуны почти целиком на оригинальных WC3-атласах
(Guldan / Priestess / …). `01/02/03/04/08–12` — в основном на `FacelessOneUnbrokenV2`
(бирюзовый ретекстур пака). Это не баг декода BLP.
Скин V2 и flame-kitbash на review **двусторонние** (`Cull Off`).

Материалы после `CreateAsset` нужно сразу `LoadAssetAtPath`, иначе Unity пишет
белый `_BaseMap` (fileID 0) — «сломанные» белые модели. Меши — `ImportAsset`
синхронно до `SaveAsPrefabAsset`, иначе у префаба `sharedMesh = null`.

JPEG BLP1 — 4 компоненты inverted BGRA (PIL видит CMYK). Декод:
`Tooling/MdxReview/blp1.py`. WC3 PNG: `extract_wc3_textures.py` из локального
`G:\Games\Warcraft III iCCup\*.mpq` в `Art/Races/Faceless/Wc3/` — только для
review, не класть в клиентский билд.

Исходники остаются в `Assets/FacelessRetexture_V2` до финализации.

Нумерация:

| # | Файл | Префаб |
|---|------|--------|
| 01 | FacelessOne_G | 01_FacelessOne |
| 02 | FacelessOneBerserker_G | 02_FacelessOneBerserker |
| 03 | RangedFacelessone_G | 03_RangedFacelessone |
| 04 | FacelessOneReaper_G | 04_FacelessOneReaper |
| 05–07 | FacelessOneSorcerer_G1..G3 | 05–07 |
| 08 | FacelessKing_G | 08_FacelessKing |
| 09 | FacelessThanatos_G | 09_FacelessThanatos |
| 10 | Unbroken_Izual | 10_Unbroken_Izual |
| 11 | FacelessOneWorker_G | 11_FacelessOneWorker |
| 12 | FacelessOneWorker_G_Portrait | 12_FacelessOneWorker_Portrait (WC3-голова) |

После маппинга номер→роль: `AssetDatabase.MoveAsset` в категории как у людей,
`_Review` удалить. Не регистрировать в `UnitVisualCatalog`, пока нет раскладки.
`SelectableRaceIds` остаётся только Люди.
