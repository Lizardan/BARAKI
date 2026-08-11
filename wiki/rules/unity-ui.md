# UI Toolkit (не uGUI)

**Не использовать** `com.unity.ugui`, Canvas или `UnityEngine.UI` для нового UI.

Стек: **UI Toolkit** + **UniRx** (`ReactiveProperty`, `ReactiveCommand`) + **UniTask** (async-флоу UI).

Биндинги: `Assets/Game/UI/Runtime/Extensions/` — мост legacy UniRx/UniTask uGUI API на `UnityEngine.UIElements.*`.

## Раскладка

```
Assets/Game/UI/Runtime/
├── Bindings/       # UIBindingScope (обязателен)
├── Extensions/     # UniRx + UniTask UITK-мост
├── Controllers/
├── ViewModels/
├── Views/
├── UXML/
└── USS/
```

PanelSettings: `Assets/Game/Settings/UI/`.

## MCP workflow (`manage_ui`)

При подключённом Unity Editor — **MCP-first** (`unity-mcp.md`). Активировать группу **ui** перед UI-работой.

1. `create` / `update` UXML и USS
2. `link_stylesheet` — подключить USS к UXML (использовать `<ui:Style>`, не голый `<Style>`)
3. `attach_ui_document` — привязать UXML к scene `UIDocument` (не править `.unity` YAML руками)
4. `get_visual_tree` — проверить размеры/позиции/цвета
5. Play mode → `render_ui` — визуальная проверка (в play mode вызывать дважды для PNG)
6. Controllers/ViewModels — **MCP-first** (`create_script`, `apply_text_edits`); файловые инструменты — только для больших рефакторов или оффлайн-редактора

## UIBindingScope (обязателен)

Каждый экран с реактивными биндингами:

```csharp
_bindingScope = new UIBindingScope(rootVisualElement);
_bindingScope.Add(viewModel.Title.SubscribeToText(titleLabel));
_bindingScope.Add(viewModel.PlayCommand.BindTo(playButton));
// OnDestroy: _bindingScope.Dispose();
```

Scope также делает dispose на `DetachFromPanelEvent`. При dispose отписка панельных колбэков — только когда `_root.panel != null`.

## Lifecycle UI-колбэков

Транзиентные колбэки регистрировать в **`OnEnable`**, снимать в **`OnDisable`** — не в `OnDestroy`
через `UIDocument.rootVisualElement` (null после detach панели → NRE).

```csharp
private VisualElement _root;

private void OnEnable()
{
    _root ??= _uiDocument.rootVisualElement;
    _root?.RegisterCallback<KeyDownEvent>(OnKeyDown);
}

private void OnDisable()
{
    _root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
}
```

## uGUI → UI Toolkit map

| Legacy uGUI (удалено) | UI Toolkit (использовать) |
|-----------------------|---------------------------|
| `Text` | `Label` / `TextElement` |
| `SubscribeToInteractable(Selectable)` | `SubscribeToEnabled(VisualElement)` |
| `InputField` | `TextField` |
| `Dropdown` (int index) | `DropdownField` + `OnIndexChangedAsObservable` |
| `ScrollRect` | `ScrollView` |

## Controller pattern

- **ViewModel:** `ReactiveProperty`, `ReactiveCommand` — без ссылок на `VisualElement`
- **Controller:** `[RequireComponent(typeof(UIDocument))]`, создаёт `UIBindingScope`, вешает биндинги
- Async-загрузка сцены: `await SceneManager.LoadSceneAsync("Game").ToUniTask(cancellationToken)` из MainMenu Play
- MainMenu фейдит на месте, затем грузит `Game.unity` (отдельная gameplay-сцена)

## UXML / USS

- Именовать элементы для `root.Q<T>("Name")`
- USS-переменные на `:root`; скрытие через `display: none` или `.menu--hidden`
- Разделять структуру (`.uxml`), стили (`.uss`) и логику (`.cs`). Стили не размещать в C#-коде
- Адаптивная вёрстка — Flexbox; избегать фиксированных `px`, если не строго необходимо (предпочитать `%`, `auto`, `flex-grow`, `flex-shrink`)
- Отступы/размеры — относительные единицы, USS custom properties и общие константы
- CSS-классы только в `kebab-case`: `.main-button`, `.card-container`, `.unit-panel-header`
- Интерактивные кнопки: плавные переходы через `transition-property` / `transition-duration` (hover, active)
- Связывание — чистый C# через `UIDocument` + `rootVisualElement.Q<T>()` / `Query<T>()`; data binding не смешивать со стилями

```csharp
var root = _uiDocument.rootVisualElement;
var playButton = root.Q<Button>("play-button");
var cards = root.Query<VisualElement>(className: "card-container");
```

```uss
:root {
    --panel-gap: 2%;
}

.main-button {
    transition-property: background-color, scale;
    transition-duration: 0.15s;
}
```
