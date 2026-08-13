# BARAKI

Мультиплеерная FFA-стратегия для Windows: строй базу, нанимай армии, которые сами идут в бой. 2–5 игроков, только PvP, без ботов.

Ремейк WC3-карты [Survival Chaos](https://www.w3sur5al.com/Home/surchaos) на Unity 6.5.

## Скачать

Последний релиз — на [GitHub Releases](../../releases/latest).

## Технологии

- Unity 6000.5.5f1, C# 12, URP 17.5
- Netcode for GameObjects (listen-host) + Unity Lobby/Relay
- UI Toolkit, UniRx + UniTask
- Cinemachine 3, Input System 1.19

## Разработка

```
Сцены: Bootstrap → MainMenu → Lobby → Game
```

Структура и соглашения — в [AGENTS.md](AGENTS.md). Дизайн-документация — в [GameDesign/](GameDesign/).