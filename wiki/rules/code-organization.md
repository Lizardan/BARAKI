# Организация кода

## Раскладка `Assets/Game/`

```
Assets/Game/
├── Art/
│   ├── Materials/      # URP Lit (level backdrop, props)
│   └── …               (Animations, Models, Textures; VFX/ — резерв)
├── Audio/              (Music, SFX)
├── Prefabs/
│   └── Cameras/        # Cinemachine rigs (GameCameraRig.prefab)
├── UI/
│   └── Runtime/        (Bindings, Extensions, Controllers, ViewModels, Views, UXML, USS)
├── Settings/
│   ├── Input/          # GameInputActions.inputactions
│   ├── UI/             # PanelSettings
│   ├── Rendering/      # RPAsset.asset, Renderer.asset, DefaultVolumeProfile.asset
│   └── ProjectBootstrap/
├── Scenes/             # Bootstrap, MainMenu, Game, Levels/, Menus/
└── Scripts/
    ├── Runtime/
    │   ├── Core/       # GameManager, GameSession, GameAudio, BootstrapSceneLoader
    │   ├── Input/      # Game.Input (генерируется)
    │   ├── Gameplay/   # Combat, Match, Networking, Cameras, Vfx, Data
    │   └── Utilities/
    ├── Editor/
    └── Tests/
```

Подпапки добавлять по потребности (`ScriptableObjects/`, `Prefabs/Characters/`, …).

## Ассемблеи

| Assembly | Namespace | Папка |
|----------|-----------|-------|
| `Game.Core` | `Game.Core` | `Scripts/Runtime/Core/` |
| `Game.Input` | `Game.Input` | `Scripts/Runtime/Input/` (генерируется) |
| `Game.UI` | `Game.UI` | `UI/Runtime/` |
| `Game.Gameplay` | `Game.Gameplay` | `Scripts/Runtime/Gameplay/` |
| `Game.Editor` | `Game.Editor` | `Scripts/Editor/` |
| `Game.Tests` | `Game.Tests` | `Scripts/Tests/` |

Новые asmdef — только когда нужен жёсткий барьер сверх этих.

## Assets и сцены

- Имена ассетов PascalCase; группировка по папкам, не по префиксам (`Prefabs/Characters/PlayerKnight.prefab`)
- Сцены: `Level01_Forest.unity`, `MenuMain.unity`, `TestPlayerMovement.unity`

## Иерархия сцены

```
--- SYSTEMS ---     # DontDestroyOnLoad (GameManager, AudioManager, …)
--- CAMERAS ---     # Main Camera + CinemachineBrain, CinemachineCamera(s)
--- LEVEL ---       # Environment, lighting, GameplayReveal, greybox
--- UI ---          # UIDocument / UI Toolkit
--- DYNAMIC ---     # runtime spawned
```

## Префабы

Группировать по ролям: `Model`, `Colliders`, `Effects`, `Audio` — не плоским списком компонентов.

## Практики

- Runtime и Editor-код — в раздельных asmdef
- ScriptableObject + EventChannel — когда растёт общий конфиг/события
- Разделение ответственности: Core, Gameplay, UI, Utilities
- Тесты в `Game.Tests` со ссылками на runtime-asmdef

## Пакеты

- `Packages/manifest.json`: UniTask (git, pinned), Cinemachine, Input System, Netcode, UGS, MCP — **без `com.unity.ugui`**
- `Assets/Plugins/UniRx/`: vendored, trimmed (см. `TRIM_NOTES.md`) — после обновления с Asset Store переприменять trim
- `Assets/Game/UI/Runtime/Extensions/`: UI Toolkit-мост для UniRx + UniTask
