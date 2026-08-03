---
doc_id: wc3_ref_unit_stats
version: 0.1
status: draft
depends_on: [units, balance]
provides: [wc3_stat_reference, balance_reference_points]
source: SurvivalChaosReborn v1.58c (war3map.w3u, извлечено из MPQ)
---

# WC3 Reference — Unit Stats

> Извлечено из карты **Survival Chaos Reborn v1.58c** (`war3map.w3u`). Всего **811** уникальных юнитов/зданий (объекты задублированы ×20 защитой карты, здесь — уникальные). Поля: HP, armor, move speed, bounty, level, attack range, cooldown. Нужно для референс-точек баланса BARAKI — **не** для копирования контента.

## Сырые файлы

- `units_table.txt` — все 811 записей (HP/AR/MS/Bounty/Lvl/Rng/CD)
- `abilities_list.txt` — 881 уникальная способность
- Источник: `c:\Users\Lizardan\Downloads\SurvivalChaosReborn v1.58c_w3p.w3x` (MPQ с offset 512, защищённая, listfile удалён, файлы не зашифрованы)

## Распределение по расам (urac)

| Race | Кол-во |
|------|--------|
| (не задано — кастом) | 346 |
| orc | 113 |
| nightelf | 107 |
| undead | 107 |
| human | 96 |
| other | 42 |

> У большинства кастом-юнитов `urac` пуст — расу назначает JASS (22 расы: Human, Orc, Undead, Night Elf, Naga, Fel Orc, Blood Elf, Demon, Gnoll, Goblin, Dwarf, Arakkoa, Satyr, Pandaren, Draconid, Tauren, Troll, Murloc, Rogue, Admiralty of Kul'Tiras, Vulpera, Venthyr).

## Типы юнитов (utyp)

| utyp | Кол-во | Комментарий |
|------|--------|-------------|
| (не задано) | 364 | боевые юниты |
| ancient,mechanical | 134 | здания |
| mechanical | 83 | здания/машины |
| ancient | 75 | древние/структуры |
| undead | 26 | |
| sapper,summoned,undead | 9 | |
| summoned | 8 | |
| human / orc / tauren | 16 | |

## Armor type

| Armor | Кол-во |
|-------|--------|
| (не задано) | 472 |
| Stone | 147 |
| Ethereal | 75 |
| Wood | 61 |
| Metal | 38 |
| Flesh | 18 |

---

## Боевые юниты с bounty (332 шт.) — ключевые точки

### Рядовые (HP ~300–900, bounty ~35–100)

| Unit | HP | AR | Rng | CD | Bounty |
|------|-----|-----|-----|-----|--------|
| Swordsman | 625 | 4 | 90 | 5 | 50 |
| Warrior (orc) | 725 | — | 500 | 5 | 50 |
| Marksman (human) | 575 | 1 | — | 5 | 50 |
| Archer (nightelf) | 500 | 1 | 275 | 5 | 50 |
| Berserker (undead) | 700 | 2 | 90 | 5 | 50 |
| Zombie | 250 | — | 575 | 5 | 18 |
| Raider (undead) | 600 | — | — | 9 | 100 |
| Marauder (human) | 600 | 3 | 575 | 9 | 70 |
| Voidwalker (undead) | 750 | 5 | 200 | 9 | 130 |
| Tauren Veteran | 850 | — | 150 | 9 | 100 |
| Fel Ravager (undead) | 650 | 5 | — | 9 | 130 |
| Sea Turtle | 1300 | 9 | 650 | 11 | 150 |
| Ancient Defender | 800 | 7 | 525 | 9 | 125 |
| Flying Machine | 1000 | 9 | 400 | 9 | 125 |
| Gyrocopter | 800 | 9 | 100 | 9 | 125 |

**Кастеры (HP ~350–500, bounty ~53–98):**

| Unit | HP | Rng | CD | Bounty |
|------|-----|-----|-----|--------|
| Mage / Wizard | 375 | 375 | 5 | 75 |
| Blood Priest (NE) | 375 | — | 5 | 75 |
| Elder Shaman (orc) | 400 | 500 | 5 | 75 |
| Alchemist (human) | 345 | 550 | 5 | 53 |
| Eredar Warlock | 375 | 525 | 5 | 98 |
| Man'ari Diabolist | 375 | 90 | — | 98 |
| Warlock (undead) | 425 | — | 5 | 75 |
| Fel Cultist | 475 | 325 | 5 | 75 |

**Осадные (HP ~800–2100):**

| Unit | HP | AR | Rng | CD | Bounty |
|------|-----|-----|-----|-----|--------|
| Siege Engine | 875 | 10 | 250 | 9 | 100 |
| Steam Tank | 475 | 3 | 500 | 5 | 50 |
| Stym Roller (human) | 2050 | 7 | 650 | 11 | 105 |
| Rocket Buggy | 1200 | 7 | 600 | 9 | 88 |
| Wolf Siege Engine | 2100 | 9 | 200 | 11 | 150 |
| Horde Warmachine | 2050 | 7 | 650 | 11 | 150 |

### Элита / чемпионы (HP ~3500–7000, bounty 1000)

| Unit | HP | AR | Bounty |
|------|-----|-----|--------|
| Adventurer | 5000 | 12 | 1000 |
| Champion | 3500 | 12 | 1000 |
| Axemaster (undead) | 6000 | 7 | 1000 |
| Chieftain | 7000 | 26 | 1000 |
| Blood Knight | 7000 | — | 1000 |
| Lich King | 7000 | 18 | 1000 |
| Vashj'ir Champion | 7000 | 18 | 1000 |
| Warden | 6000 | 11 | 1000 |
| Shadow Dancer (NE) | 6000 | 11 | 1000 |
| Amani Shieldmasta | 7000 | 16 | 1000 |
| Grand King (human) | 4000 | 12 | 1000 |
| Last Guardian | 4000 | 12 | 1000 |

### Боссы (HP 10000–23000, bounty 1500–2750)

| Unit | HP | AR | Rng | CD | Bounty |
|------|-----|-----|-----|-----|--------|
| Abyssal | 20000 | 26 | 150 | 91 | 2750 |
| Ancient Giant | 20000 | 26 | 225 | 91 | 2750 |
| Ancient Hydra | 18000 | 21 | 350 | 91 | 2750 |
| Arachnathid King | 20000 | 27 | 175 | 91 | 2750 |
| Phoenix Chimaera | 18000 | 21 | 350 | 91 | 2750 |
| Dragon Turtle | 23000 | 31 | 150 | 91 | 2750 |
| Legion's Champion | 20000 | 27 | 100 | 91 | 2750 |
| Goblin Robot | 20000 | 26 | 175 | 91 | 2750 |
| Archdemon | 18000 | 22 | 350 | 91 | 2750 |
| Doom Lord | 10000 | 16 | 100 | 91 | 1500 |
| Siege Fortress | 18000 | 21 | 350 | 91 | 2750 |

### Здания (структуры базы, hp для референса)

| Unit | HP | AR |
|------|-----|-----|
| Ancient of War (Lv1..4) | 2000→4250 | 0→6 |
| Pandarian Hall (Lv2..4) | 2750→4250 | 2→6 |
| Tree of Life (Lv2..3) | 10000→15000 | 4→8 |
| Pandarian Sanctuary (Lv2..3) | 10000→15000 | 4→8 |
| Misfortune Ward | 90000 | 4 |

---

## Наблюдения для баланса BARAKI

1. **Коэффициент bounty/HP** растёт с редкостью юнита:
   - Рядовые: bounty ≈ HP × 0.08–0.2 (Zombie 250HP/18g — 0.07; Swordsman 625HP/50g — 0.08)
   - Элита: bounty ≈ 1000 фикс при HP 3500–7000 (0.14–0.29)
   - Боссы: 2750 при HP 18000–23000 (0.12–0.15)
   - В BARAKI `UNIT_*_MELEE` bounty 8 при HP 120 — **коэффициент 0.067**, т.е. на ~15% ниже референс-минимума WC3.
2. **Move speed = 240 у почти всех** (в т.ч. осадных). BARAKI дифференцирует MS (2.0–5.5) — это ок, но аргумент за то, чтобы осадные были медленнее рядовых, сохраняется.
3. **Armor** у рядовых 0–5, у боссов 16–31. Профиль роста сильно круче HP-профиля.
4. **Cooldown** у боссов 91 — примерно ×18 от рядовых (5), при HP ×30–70. Т.е. DPS босса сознательно занижен относительно танковости.
