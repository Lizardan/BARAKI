# BARAKI — Architecture

RTS-ремейк WC3-карты SurvivalChaos: FFA 2–8, игрок строит базу, юниты автономны.
listen-host (host-as-server) + Netcode for GameObjects + UGS (Lobby/Relay/Friends/Cloud Save).

## Tech stack

| Слой | Стек |
|------|------|
| Unity | 6000.5.5f1 (`F:\Unity\Editor\6000.5.5f1`), C# 12 |
| Rendering | URP 17.5 — `Settings/Rendering/RPAsset.asset`, `Renderer.asset`, `DefaultVolumeProfile.asset` |
| Cameras | Cinemachine 3.1 — `Gameplay/Cameras/` |
| UI | UI Toolkit only (UXML/USS/UIDocument) — **без** `com.unity.ugui` |
| Reactive | UniRx (vendored, trimmed — `Assets/Plugins/UniRx/`) |
| Async | UniTask (git, pinned); bootstrap `Game.Core` — `Awaitable` |
| Input | Input System 1.19 — `Settings/Input/GameInputActions.inputactions` |
| Multiplayer | Netcode for GameObjects 2.13 + UGS Auth 3.7 / CloudSave 3.4 / Friends 1.2 / Multiplayer 2.2 |
| Прочее | Clipper2Lib |

**Не установлены** (не ссылаться в правилах и коде): VFX Graph, ProBuilder, glTFast,
Addressables, DOTween, Roslyn. Единственный MCP-optional-dep — Cinemachine.

## Асинхронность

`Awaitable` — только bootstrap (`Game.Core`). Gameplay/UI — `UniTask`/`UniTaskVoid`.
Никогда `Task`/`async void`. Детали: `rules/unity-async.md`.

## Сцены

`Bootstrap` (build 0) → `MainMenu` (1) → `Lobby` → `Game`.

Иерархия сцены:

```
--- SYSTEMS ---     # DontDestroyOnLoad (GameManager, AudioManager, …)
--- CAMERAS ---     # Main Camera + CinemachineBrain, CinemachineCamera(s)
--- LEVEL ---       # Environment, lighting, GameplayReveal, greybox
--- UI ---          # UIDocument / UI Toolkit
--- DYNAMIC ---     # runtime spawned
```

## Модули (asmdef = namespace)

| Assembly | Папка |
|----------|-------|
| `Game.Core` | `Scripts/Runtime/Core/` |
| `Game.Input` | `Scripts/Runtime/Input/` (генерируется из `inputactions`) |
| `Game.Gameplay` | `Scripts/Runtime/Gameplay/` |
| `Game.UI` | `UI/Runtime/` |
| `Game.Editor` | `Scripts/Editor/` (в т.ч. `Mcp/`) |
| `Game.Tests` | `Scripts/Tests/` |

`Game.Gameplay` ссылается: Game.Core, Game.Input, Unity.Cinemachine, Unity.InputSystem,
Netcode, UGS, UniTask, UniRx, Clipper2Lib. Детали раскладки — `rules/code-organization.md`.

## Cameras

Все игровые камеры — Cinemachine 3 (`Unity.Cinemachine`), не ручная анимация `Camera.transform`.
Main Camera несёт `CinemachineBrain`. Runtime-привязка слежения — `GameplayCameraBinder`
(`Scripts/Runtime/Gameplay/Cameras/GameplayCameraBinder.cs`); rig-префаб `Prefabs/Cameras/GameCameraRig.prefab`
оставляет `_followTarget` null — сцена привязывает Player root. Не добавлять `CameraTarget`-ребёнка:
визуальный bob остаётся на `Model`, Y корня Player физически стабилен.

## Input

`Settings/Input/GameInputActions.inputactions` → генерируется C#-класс в `Scripts/Runtime/Input/`
(namespace `Game.Input`). Подписки в `OnEnable`, отписки в `OnDisable`. Геймплей читает ввод через
сгенерированный класс, не `UnityEngine.Input`.

## MCP

Unity Editor → сервер `unityMCP` (мост `:6400`). MCP-first для editor-операций.
Воркфлоу и инструменты: `rules/unity-mcp.md`.
