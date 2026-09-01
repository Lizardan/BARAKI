# BARAKI — Architecture

Мультиплеерная FFA-стратегия BARAKI: 2–5 игроков, игрок строит базу, юниты автономны.
listen-host (host-as-server) + Netcode for GameObjects + UGS (Lobby/Relay/Friends/Cloud Save).

## Tech stack

| Слой | Стек |
|------|------|
| Unity | 6000.6.0f1 (`F:\Unity\Editor\6000.6.0f1`), C# 12 |
| Rendering | URP 17.6 — `Settings/Rendering/RPAsset.asset`, `Renderer.asset`, `DefaultVolumeProfile.asset` |
| Cameras | Cinemachine 6.6 (CM3 API, builtin) — `Gameplay/Cameras/` |
| UI | UI Toolkit only (UXML/USS/UIDocument) — **без** `com.unity.ugui` |
| Reactive | UniRx (vendored, trimmed — `Assets/Plugins/UniRx/`) |
| Async | UniTask (git, pinned); bootstrap `Game.Core` — `Awaitable` |
| Input | Input System 1.20 — `Settings/Input/GameInputActions.inputactions` |
| Multiplayer | Netcode for GameObjects 2.13 + UGS Auth 3.7.4 / CloudSave 3.4 / Friends 1.2 / Multiplayer 2.3 |
| Прочее | Clipper2Lib |

**Не установлены** (не ссылаться в правилах и коде): ProBuilder, glTFast,
Addressables, DOTween, Roslyn. VFX Graph **установлен** (17.6) — Adjustable Slash pack + тинт
`AbilityVfxTint`. Единственный MCP-optional-dep — Cinemachine.

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
Netcode, UGS, UniTask, UniRx, Clipper2Lib, Unity.VisualEffectGraph.Runtime. Детали раскладки — `rules/code-organization.md`.

## Cameras

Все игровые камеры — Cinemachine 6.6 / CM3 API (`Unity.Cinemachine`: `CinemachineCamera`,
`CinemachineFollow`, `CinemachineBrain`), не ручная анимация `Camera.transform`.
Main Camera несёт `CinemachineBrain`. Runtime-привязка слежения — `GameplayCameraBinder`
(`Scripts/Runtime/Gameplay/Cameras/GameplayCameraBinder.cs`); rig-префаб `Prefabs/Cameras/GameCameraRig.prefab`
оставляет `_followTarget` null — сцена привязывает Player root. Не добавлять `CameraTarget`-ребёнка:
визуальный bob остаётся на `Model`, Y корня Player физически стабилен.

Предпочтительный край экрана для своей базы (`CameraBaseScreenEdge`) хранится в
`GameplayCameraPreferences` (PlayerPrefs, по умолчанию низ). Главное меню: клик по краю большой
схемы режима; старт матча и pad камеры берут то же значение.

## Input

`Settings/Input/GameInputActions.inputactions` → генерируется C#-класс в `Scripts/Runtime/Input/`
(namespace `Game.Input`). Подписки в `OnEnable`, отписки в `OnDisable`. Геймплей читает ввод через
сгенерированный класс, не `UnityEngine.Input`.

## MCP

Unity Editor → сервер `unityMCP` (мост `:6400`). MCP-first для editor-операций.
Воркфлоу и инструменты: `rules/unity-mcp.md`.

## Unity 6.6

**Взяли:** UITK `drop-shadow` на панелях; `backdrop-filter` только поверх UITK (меню/лобби), не поверх 3D;
managed-code defines вместо `DEVELOPMENT_BUILD`; Burst built-in; Cinemachine 6.6 как core
package (API CM3). CI: `unityci/editor:windows-6000.6.0f1-windows-il2cpp-3`.

**Backlog (не внедрять без нужды):** DXC для DX12 в Shader Build Settings; shader constants
per Build Profile; `Unity.AI.Navigation.LowLevel` jobs; `NetworkTransportInterface`;
`[SerializeField] Dictionary` на новых SO; UITK mesh modifiers / vertex Shader Graph для HUD.

**Не использовать:** On-Tile / Tile-Only post (мобайл), `com.unity.feature.2d`, кастомные
UITK-вершины для chrome, SafeArea uGUI, Content Directories / Addressables.
