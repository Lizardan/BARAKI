---
doc_id: todo
version: 0.8
status: locked
depends_on: [vision, technical, map_topology, platform]
provides: [backlog, priorities, acceptance_criteria]
---

# TODO

> **Для агента:** PvP-only, без ботов. Windows + Lobby/Relay. **TDD:** тесты → код → `run_tests` → done только при green.
> **Гейт расы #2:** PRE-001..007 (blessing abilities → бонусы → tower×9) → GATE playtest/checkup → только потом EA-001.

---

## Phase 0 — GDD

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| GDD-001 | Структура GameDesign | done | |
| GDD-002 | Топология 2–5 | done | |
| GDD-003 | PvP-only, уникальные расы | done | |
| GDD-010 | Пивот: Windows hub + Lobby/Relay + GitHub Releases | done | Platform.md; Vision/Technical/TODO updated |

---

## Phase 1 — MVP online (Windows host-as-server)

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| MVP-N10 | UGS Auth + Lobby + Relay packages | done | multiplayer/auth/friends/cloudsave in manifest |
| MVP-N11 | `UnityLobbyRelaySessionBackend` + StartAsHost | done | relay-host/relay endpoints; AllocationUtils |
| MVP-N02 | Lobby slots / ready / start | done | NetworkLobbyState + mid-match reserve |
| MVP-N03 | Server-authoritative gold + spawn | done | Snapshots + command RPCs + HUD gold |

### Map / data / match (carry-over)

| ID | Task | Status |
|----|------|--------|
| MVP-001..004 | Arena / LaneGraph / greybox | done |
| MVP-010..012 | 2 races SO | done |
| MVP-020..022 | MatchController / waves / gold | done |
| MVP-023 | Elimination + disconnect grace | done |
| MVP-024 | Hero summon + tower targeting | done |
| MVP-030..033 | Match HUD / results / selection | done |

---

## Phase 1b — Hero system (XP / abilities)

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| HERO-001 | TT_King = слот 1 (HERO_HUMAN_1) | done | GDD Heroes.md; UnitVisualCatalog hero1 prefab = Human_Hero1 (TT_King + TtUnitTeamColor + Human_Hero1.controller) |
| HERO-002 | XP/Level: HeroLevelRules, слот-уровень, XP за килл героем и за здания | done | HeroLevelRulesTests, HeroMatchControllerTests |
| HERO-003 | Рост статов с уровнем + редеплой с сохранённым уровнем | done | ResolveHeroStats tests |
| HERO-004 | Снапшот v11: HeroLevel/HeroXp/HeroXpNext | done | snapshot tests |
| HERO-005 | 4 способности (1/4/7/10): авто-каст + VFX + аура + самобафф ульта | done | HeroAbilityRulesTests |
| HERO-006 | UI ContextStrip: уровень + XP-прогресс | done | hero strip shows TT_King, level, XP progress; formatting tests |
| HERO-007 | Уникальные киты героев 2–3 + VFX | done | Paladin Smite/Shield/AS aura/Consecration; Priest Nova/GreaterHeal/armor aura/Revive; те же unlock 1/4/7/10 |

---

## Phase 2 — Hub + social

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| HUB-001 | Main Menu info hub layout | done | Hub panel in MainMenu.uxml |
| HUB-002 | Cloud Save profile (nick; rank/points stub) | done | PlayerProfileService |
| HUB-003 | UGS Friends + presence | done | FriendsHubService |
| HUB-004 | Invite friend → game lobby | done | Presence lobbyCode + join code |
| HUB-005 | Left dock: chat / history / public games / settings | done | UI shell; chat local-only; lists placeholder |

---

## Phase 3 — CI + force update

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| DIST-001 | GHA tag `v*` → Windows zip → GitHub Release | done | deploy-windows.yml |
| DIST-002 | In-game version check + block Play | done | GameUpdateService + hub gate |
| DIST-003 | Download + ApplyUpdate.bat restart | done | Tooling/BuildSupport/ApplyUpdate.bat |

---

## Phase 4 — Resilience (post-MVP)

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| RES-001 | Mid-match host migration (pause → full state → resume) | done | Pause/elect/capture/rebinding phases; Relay rebind notify hooked |
| RES-002 | Player reconnect into active match | done | Pause overlay + kick/return + session token ClaimReconnect |

---

## Phase PRE-RACE2 — до расы #2

> Реализация по одному ID. **Следующий:** PRE-006 (12 бонусов Людей). **Раса #2 (EA-001) не начинать**, пока не закрыты **PRE-001..007** и **Phase GATE**. Старые 5 Human tower tracks выкинуты из канона — invent **9 новых**.

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| PRE-001 | Бонус-оверлей 60с + random timeout + скрытость | done | Каркас UI; 12 пустых слотов; матч не паузится; скрытность = UI-only (чужие пики не отображаются, данные технически у всех), пики в снапшоте v13 (reconnect/migration-safe); см. `Bonuses.md` |
| PRE-002 | Титан: полоска над main + выпуск как герой | done | Полоска **180 s** над main (main L3 + все 3 героя IdleAtBase; deployed/dead → заморозка без сброса) → титан появляется на базе; выпуск из живого barracks **2500g**; CD после смерти **300 s** как герой (без повторной полоски); XP как герой; статы сид **3×** на префаб `Human_Titan`; снапшот v15 (титан + ростер героев); см. `Heroes.md` |
| PRE-003 | Divine Blessing (FoW off) + меню 1 способности main | done | Research main L2+ (**1000g / 45s**); FoW off для себя; отложенный выбор 1 stub ability (гейты main tracks); эффекты abilities — **PRE-005**; снапшот v17; см. `Upgrades.md` |
| PRE-004 | Контент героев слот 2–3 (baseline) | done | Имена TT_Mounted_Paladin / TT_Mounted_Priest; визуал Human_Hero2/3; статы baseline на префабе (`UnitCombatSettings`); 3 hero-бонуса больше не пустые по контенту |
| PRE-005 | Main extra abilities после Divine Blessing | done | Меню 2×3; **2** способности (Кара зданий 1200 / Кара юнитов 5000; CD 180; 200 mana; общий гейт melee+ranged+armor≥7 + magic≥2); слоты 3–6 stub; main mana; HP main/barracks по уровню; снапшот v18; тесты |
| PRE-006 | Наполнение всех 12 бонусов Людей | pending | Invent + внедрить **все 12** слотов (10 replacement + 2 unique): не пустые кнопки; эффекты работают в матче; стакаются с tower upgrades; late game ~40–60 мин — мощная армия; см. `Bonuses.md` |
| PRE-007 | Tower upgrades ×9 (Humans) invent + implement | pending | **9 новых** треков (старые 5 scrap); только **юниты** (не герои/титан/DPS башен); L1→L2→L3 (без L1 нет L2); 4 башни → до 4 разных треков; UI: убрать stub слота 1, треки на **слотах 4–12**; удалить старые `GameIds`/entities; тесты; см. `Races.md` / `Upgrades.md` |

---

## Phase GATE — Playtest + checkup (перед расой #2)

> После полного PRE-001..007. Без закрытия GATE — **EA-001 не начинать**.

| ID | Task | Status | Acceptance |
|----|------|--------|------------|
| GATE-001 | Playtest на Людях (полный контент) | pending | Серия матчей: blessing abilities, бонусы, tower×9, титан, netcode/migration/reconnect; баги заведены или закрыты |
| GATE-002 | Полный чекап проекта | pending | EditMode green; консоль без ошибок на smoke Bootstrap→Menu→Lobby→Match; релизный Windows-билд при необходимости |

---

## Phase EA — Early Access content

| ID | Task | Status |
|----|------|--------|
| EA-001 | Раса #2 | deferred | **Blocked:** PRE-001..007 + GATE-001..002 |
| EA-002 | Lobby N=3,5 casual | deferred |
| EA-004 | Ranked Duel (N=2) | deferred |
| EA-005 | Ranked FFA4 (N=4) | deferred |

---

## Log

| Date | Note |
|------|------|
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
| 2026-08-14 | PRE-002: титан — полоска 180s над main (3 героя IdleAtBase) → idle на базе; выпуск 2500g с казарм как герой; CD 300s без повторной полоски; снапшот v15 (ростер героев + titan level/xp/CD + HeroSlot/IsParkedAtBase) |
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
