# BARAKI

Мультиплеерная FFA-стратегия для Windows: строй базу, нанимай армии, которые сами идут в бой. 2–5 игроков, только PvP, без ботов.

Оригинальная игра на Unity 6.6 в духе Tug of War / Castle Fight.

## Скачать

Последний релиз — на [GitHub Releases](../../releases/latest).

## Технологии

- Unity 6000.6.0f1, C# 12, URP 17.6
- Netcode for GameObjects (listen-host) + Unity Lobby/Relay
- UI Toolkit, UniRx + UniTask
- Cinemachine 6.6 (CM3 API), Input System 1.20

## Разработка

```
Сцены: Bootstrap → MainMenu → Lobby → Game
```

Структура и соглашения — в [AGENTS.md](AGENTS.md). Дизайн-документация — в [GameDesign/](GameDesign/).