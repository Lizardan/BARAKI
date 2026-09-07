---
doc_id: todo
version: 0.9
status: locked
depends_on: [vision, technical, map_topology, platform]
provides: [backlog, priorities, acceptance_criteria]
---

# TODO

> **С 2026-09-03 задачи ведутся в HacknPlan** ([проект 242091](https://app.hacknplan.com/p/242091/dashboards/project)). Этот файл — карта фаз и лог, не бэклог.
>
> - Что делать сейчас: board **Now** в HacknPlan / панель UnioTasks
> - История GitHub: [закрытые issues](https://github.com/Lizardan/BARAKI/issues?q=is%3Aclosed+is%3Aissue) (архив, не трогать)
> - Правила: `wiki/rules/github-issues-workflow.md`
>
> Стадии HacknPlan: Planned = очередь · In progress = в работе · Testing = ждёт апрува · Completed = готово.

**Для агента:** PvP-only, без ботов. Windows + Lobby/Relay. **TDD:** тесты → код → `run_tests` → done только при green.
**Гейт расы #2:** PRE-001..007 (blessing abilities → бонусы → tower×9) → GATE playtest/checkup → только потом EA-001.
Текущее состояние: PRE-001..007 и MAIN-001 **сделаны**. Открытая очередь: **GATE-001**, **GATE-002**, затем EA-*.

---

## История изменений задач

До версии 0.9 задачи велись таблицами прямо здесь (Phase 0 GDD → Phase 1 MVP → 1b Heroes → 2 Hub → 3 CI/Distribution → 4 Resilience → PRE-RACE2 → GATE → EA). Полная история перенесена в закрытые issues с сохранением ID-префиксов (`GDD-*`, `MVP-*`, `HERO-*`, `HUB-*`, `DIST-*`, `RES-*`, `PRE-*`, `GATE-*`, `EA-*`, `CHAT-*`) — см. ссылку «Закрытая история» выше.

---

## Log

| Date | Note |
|------|------|
| 2026-09-08 | **FACELESS-010 (дизайн)**: бонусы Древних юнит за юнитом + уники — все 12 слотов утверждены пользователем (сессия). Melee=Hunger of the Old One (on-hit 15%, вампиризм 50% урона), Ranged=Tainting Bolt (дот 3/с×3), Caster=Call of the Abyss (on-kill 100%: мини-меле ×0.5 статов Melee, масштаб ×0.67), Siege=Death Explosion (10% max HP, r3, вражеские юниты), Flying=Hungering Flight (+15% AS on-kill, стаки до 3), Super=Feast on the Fallen (+80 HP / +10% AS on-kill), Hero1=Ancient Mantle (+50% dmg/+2 брони 8 с, ульт-замена), Hero2=Area of Miss (враги в r5 на 4 с промахиваются 100%), Hero3=Feast Zone (вампиризм-зона 10 с, 30% урона), Titan=Aura of Hunger (армия лечится 15% от урона), Уник 11=Shadow of the Void (войска 8% избегают атаки), Уник 12=Void Bastion (здания владельца 20% промаха). Канон: `wiki/rules/faceless-unit-bonuses.md`. Runtime-гейт (`HasBonusKit=false`) сохранён — реализация отдельными follow-up карточками; полный asymmetry kit (пассивы/магия/треки) → FACELESS-008. Правки документации, без кода. |
| 2026-09-08 | **FACELESS-007**: здания Древних (Nazjatar Houses by Ageron). MDX v800×5 из `Assets/Nazjatar_Houses_By_Ageron` → статик-меши (`FacelessMdxDocument`, без скиннинга) + 35 BLP1→PNG (`Tooling/MdxReview/convert_nazjatar_buildings_textures.py`). Маппинг пользователя: **04=Main, 05=Tower, 03=Barracks** (01/02 не исп.). Production-префабы `Prefabs/Races/Faceless/Buildings/{TownHall,Tower,Barracks}` (root + `Model` со скейлами пользователя + yaw 270 + `Foundation` — процедурный цоколь + `FacelessUnitTeamColor`); эталоны из `Scenes/Dev/TestBuildings.unity`. `BuildingVisualCatalog` race-keyed (`TryGetPrefab(buildingId, raceId)`, fallback Human); greybox `Populate(+raceIds)` ← `MatchArenaGreybox.Configure(+raceIds)` ← `MatchRuntime`. `BUILDING_SET_FACELESS` в GameIds. EditMode **1278 passed** (+6 `BuildingVisualCatalogRaceTests`). Скрин: `Assets/Screenshots/faceless_production_vs_human.png`. Следующее: FACELESS-010 (бонусы), FACELESS-008 (asymmetry kit), GATE. |
| 2026-09-07 | **FACELESS-006**: верификация анимаций Древних + race-aware тайминги удара. Все 10 production-префабов: контроллер рядом, `applyRootMotion=false`, состояния Stand/Walk/Attack/Death/Cast привязаны, `FacelessUnitTeamColor`+`UnitCombatSettings` на месте. Поликаунт ок (max Titan 2843 verts, остальные <1.2k, декimation не нужен). `AbilityAnimRules.ResolveAttackClipSeconds(+raceId)`: для `RACE_FACELESS` — фактические длины клипов MDX v800 (таблица в `wiki/rules/faceless-assets.md`), Human не изменился; presenter прокидывает raceId через `ResolveRaceId`. Тесты EditMode **1263 passed**. Следующее: **FACELESS-010** (бонусы), затем **FACELESS-007** (здания Древних) и GATE. |
| 2026-09-06 | **FACELESS-001..009**: раса #2 (Древние) играбельна. Контент: MDX→меши/скины, префабы `Units/`+`Heroes/` (production+боевые), SO (`RACE_FACELESS`, 6 юнитов, 3 героя), портреты, каталоги; статы = Human роль→роль; титан = hero1×3. Runtime-гейты Фазы 1: кастер-кит пустой для Faceless (`CreateForSpawn(raceId,…)`), бонус-пик нейтрализован (`HasBonusKit=false`, `EffectiveBonusSlot*` race-aware), tower-треки глобальны (работают). `_Review/` удалён. Тесты EditMode green (кроме pre-existing minimap UI-004). Этапы текущей фазы ведутся в HacknPlan. Следующее: **FACELESS-010** (бонусы Древних, юнит за юнитом) и GATE. |
| 2026-08-26 | MAIN-001 (#50): способности главного здания — Ледяное кольцо (L1+, ground-target AoE ~5, ~300 dmg + freeze 3s, 50 mana, CD 60) и Волна света (L2+, волна от базы r~17, ~1500 dmg — плейсхолдеры, 150 mana, CD 180). Вне меню Divine Blessing, гейт по уровню здания. Вставлена до PRE-007 |
| 2026-08-25 | Трекинг перенесён в GitHub Issues (мастер-issue #47, метки `todo-task` / `phase/*`). Правила: `wiki/rules/github-issues-workflow.md`. Пуш консольных логов в issues убран из дебаг-консоли |
| 2026-08-25 | PRE-006b: слоты 7–12. Ветераны 7–10 (тот же кит ×1.35 + сигнатура: King's Command 56 / Aegis 57 / Sanctuary 58 / Greater Colossus 59; статы ×1.4/×1.35/+2 armor; morale +15%; флаг TT_RTS_Banner_plain на спине; префабы BonusHeroes/, портреты автопекутся) и уники 11–12 (March Discipline +10% скорости всем войскам; Stone Masonry +20% HP зданий ретро-пропорционально). UI оверлея: все 12 слотов активны. Правило контента: имя EN / описание RU. Следующий — PRE-007 |
| 2026-08-24 | Снапшот wire v21: секционный формат, static/dynamic split, StringTable, EventStream; handshake версии в лобби, graceful decode failure, checksum-resync/kick. Контракт презентации — `wiki/rules/snapshot-wire.md`. Починен melee-FX host-only баг (target в wire). CHAT-фиксы: JSON control-chars, изоляция poll-запросов, строгий 2xx |
| 2026-08-17 | Контент-раскладка: категории `Units` / `BonusUnits` / `Heroes`(+Titan); `BonusHeroes` зарезервирован; канон в `wiki/rules/content-assets.md` |
| 2026-08-15 | PRE-005 VFX: Кара зданий/юнитов — `FxKind.SkyBeam` луч с неба; sync через SpellCasts (`AbilityIds` 100/101) |
| 2026-08-15 | PRE-005: меню 2×3; Кара зданий (1200) / Кара юнитов (5000); гейт melee+ranged+armor≥7 + magic≥2; CD 180 / 200 mana; main mana 100×level; HP main/barracks по уровню; снапшот v18 |
| 2026-08-15 | Порядок PRE: **PRE-005** = blessing abilities (доделать Divine Blessing) → **PRE-006** = 12 бонусов → **PRE-007** = tower ×9; следующий = PRE-005 |
| 2026-08-15 | Split: бонусы и blessing abilities — разные ID; гейт EA = PRE-001..007 + GATE |
| 2026-08-15 | PRE-003: Divine Blessing 1000g/45s (main L2+); FoW off владельцу; меню stub abilities 1–6 с гейтами; pick 1×; снапшот v17; magic costs → 500/750/1000g; боевые эффекты abilities → PRE-005 |
| 2026-08-15 | Гейт расы #2: PRE-005 blessing → PRE-006 бонусы → PRE-007 tower ×9; Phase GATE; EA-001 blocked до PRE+GATE |
| 2026-08-15 | ScriptableObjects: owner-first раскладка (`Heroes/HeroN`, `Units/Caster|Titan` + `Abilities/`); пустой scaffold убран; канон в `wiki/rules/content-assets.md` |
| 2026-08-15 | `UnitCombatSettings`: единый prefab-компонент статов и abilities; ScriptableObjects перенесены в race-first структуру; каталоги переведены на расширение по `raceId` |
| 2026-08-14 | Способности на префабе: каст по типу; Priest Nova по союзнику, Greater Heal — зона 10с; титан Slam/Rally/Colossus/Stomp |
| 2026-08-14 | PRE-004: герои 2–3 (TT_Mounted_Paladin / TT_Mounted_Priest) и титан (TT_Peasant); префабы Human_Hero1/2/3 + Human_Titan; боевой профиль на `UnitCombatSettings`; каталог по слоту |
| 2026-08-14 | PRE-002: титан — полоска 180s над main (3 героя IdleAtBase) → idle на базе; выпуск 2500g из barracks как герой; CD 300s без повторной полоски; снапшот v15 (ростер героев + titan level/xp/CD + HeroSlot/IsParkedAtBase) |
| 2026-08-14 | Titan механика финализирована: пассивное изучение 180s (main L3 + 3 героя на базе, заморозка при deployed/dead) вместо hire; summon 2500g из barracks, CD 300s, статы 3× героя; GDD обновлён (Heroes/Buildings/Economy/Units/Bonuses/AI/Core/Upgrades) |
| 2026-08-14 | GDD hygiene + каркас Bonuses/Titan/Divine Blessing; Phase PRE-RACE2 |
| 2026-08-12 | Hero visual: Human_Hero.prefab (guid ac87b9a4) заменён с greybox-примитивов на nested TT_King + TtUnitTeamColor (4 текстуры команд) + Animator (Human_Melee.controller, ApplyRootMotion=off); тесты green (803 passed) |
| 2026-08-11 | Hero system (XP/levels): TT_King = слот 1; XP за убийства юнитов героем + убийства зданий твоими юнитами; уровень слота переживает смерть/redeploy; статы растут с уровнем; 4 способности (удар/хил/аура/ульта) на 1/4/7/10, самобафф ульта +50%/8с; снапшот v11 (HeroLevel/HeroXp/HeroXpNext) |
| 2026-08-11 | Caster spell VFX + Frost freeze: событие каста уходит в снапшот (**v9** `SpellCasts`, serial-dedup) → клиентский presenter; лейбл названия над кастером, зелёный/жёлтый «+» (Heal над HP-полоской, Resurrect на месте возрождения), синий AoE-круг Frost, подъём+fade (primitives + legacy TextMesh, URP Unlit); Frost замораживает жертв **1,5s** (full stun — `Frozen` behavior, не двигается/не атакует); EditMode green (772+ тестов) |
| 2026-08-11 | Caster mana economy: заклинания тратят ману (**50/75/150**), каст только при достаточной мане, реген **5/с** (пул 200, clamp); `Mana` сериализована в снапшот (v8) — клиентская полоска маны теперь корректна; GDD Balance/Economy синхронизированы |
| 2026-08-11 | Caster spells implemented: `UNIT_TYPE_CASTER` авто-кастует Heal/Frost/Resurrect (`UPG_MAIN_MAGIC` slot 1/2/3; приоритет Heal→Frost→Resurrect; per-caster CD; transient host корпусы ≤20s; событие `SpellCast` для VFX); каждый слот +3 dmg автоатаки (`MagicDamagePerLevel`); `UPG_CASTER_HEAL` убран из дизайна и кода; полный EditMode green (769 тестов) |
| 2026-07-20 | Playtest UX: barracks manual call (gold+charges), own timers only, building HP bars, idle hero park, defensive building auto-fire + RMB target, tooltips on top; race pick Humans-only (Bugs grey) |
| 2026-07-20 | Listen-host polish: fixed 30 Hz tick, client unit lerp, command ack/fail UI, host migration end-to-end + last-good snapshot, debug checksum |
| 2026-07-20 | MVP online close-out: command RPCs + snapshot gold/HUD, disconnect grace, heroes/towers, results rematch/menu, host-migration/reconnect wiring |
| 2026-07-16 | Full GitHub migration: Releases + Pages |

---

## User decisions

- [x] Windows Standalone
- [x] Один проект, Main Menu = hub (без отдельного лаунчера)
- [x] Host-as-server + Unity Lobby + Relay
- [x] Friends + Cloud Save (UGS)
- [x] GHA → GitHub Releases → force update
- [x] Full host migration + reconnect — фундамент listen-host (не optional)
- [x] Playtest race gate — только Люди (Жуки disabled)
- [x] Уникальные расы, без ботов, 2–5 игроков
- [x] Бонус после race pick — каркас UI (PRE-001); полный контент = PRE-006
- [x] До расы #2: blessing abilities (**PRE-005**) → 12 бонусов (**PRE-006**) → tower ×9 (**PRE-007**) → playtest/checkup → только потом EA-001
- [x] Старые 5 Human tower tracks scrap; 9 новых; UI башни слоты 4–12; stub слота 1 убрать
- [x] Tower upgrades влияют только на юнитов (не герои/титан); бонусы стакаются с tower; late game ~40–60 мин
- [x] Задачи ведутся в GitHub Issues (мастер-issue #47), TODO.md — только ссылки + лог
