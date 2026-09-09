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

`Esc` при фокусе в `TextField` часто не всплывает (bubble-up). Для отмены режима ввода регистрировать
`KeyDownEvent` с `TrickleDown.TrickleDown` (как в `MainMenuController` для join-кода).

## Запросы элементов и ассет UXML (два грабля)

1. **Не назначенный `visualTreeAsset`** в сцене: UIDocument есть, UXML не привязан → `rootVisualElement`
   пустой, все `Q<T>()` дают `null`, окно не рендерится (симптом — NRE на первом же
   не-защищённом `.text = `/`.Clear()`, например `BonusPickController.LateUpdate`). Проверять
   в edit/play, что у каждого scene-UIDocument (`MatchHud`, `RacePick`, `BonusPick`, `HeroOrderPick` в
   `Game.unity`) поле `visualTreeAsset` заполнен (например, через `execute_code`).
2. **Запросы в `Awake`** могут выполниться до импорта дерева UIDocument (импорт — на enable,
   порядок с колбэком не гарантирован) → поля остаются `null`. Не полагаться на один `Awake`;
   вынести lookup в переиспользуемый `ResolveElementReferences()` и пере-резолвить в `LateUpdate`,
   пока сентинел `== null` (как в `BonusPickController`/`HeroOrderPickController`).

## uGUI → UI Toolkit map

| Legacy uGUI (удалено) | UI Toolkit (использовать) |
|-----------------------|---------------------------|
| `Text` | `Label` / `TextElement` |
| `SubscribeToInteractable(Selectable)` | `SubscribeToEnabled(VisualElement)` |
| `InputField` | `TextField` |
| `Dropdown` (int index) | `DropdownField` + `OnIndexChangedAsObservable` |
| `ScrollRect` | `ScrollView` |

## Pointer / screen → panel

Input System (`Mouse.position`) — origin **снизу слева**; UI Toolkit `RuntimePanelUtils.ScreenToPanel` —
origin **сверху слева**. Всегда конвертировать через `MatchSelectionUiPointer.ScreenToPanelPosition`
(или `ToUiToolkitScreenPosition`), иначе элементы у курсора (напр. `TargetingTooltip`) «зеркалятся» по Y.

## Controller pattern

- **ViewModel:** `ReactiveProperty`, `ReactiveCommand` — без ссылок на `VisualElement`
- **Controller:** `[RequireComponent(typeof(UIDocument))]`, создаёт `UIBindingScope`, вешает биндинги
- Async-загрузка сцены: `await SceneManager.LoadSceneAsync("Game").ToUniTask(cancellationToken)` из MainMenu Play
- MainMenu фейдит на месте, затем грузит `Game.unity` (отдельная gameplay-сцена)

## Launcher / Main Menu (визуальный язык)

Лаунчер и главное меню — **graphite dark-fantasy**: фон `#080908`, панели `#101211`/`#151716`, рамки `#343832`, текст `#DEDBD2`, бронза `#B99A62`. Шрифт — существующий `NotoSans` (без serif).

- **Launcher:** окно всегда **1280×720** (`LauncherPanelSettings`, reference 1280×720) — UI не сжимается с 1080p, масштаб ~1.5× относительно старого 1920-panel. Сетка `header 74px / news+chat / footer 76px`. В шапке только жирный `BARAKI` и версия справа, без слогана. Статус футера — одна линия: точка + текст, полоса `flex-grow` как связка, процент справа; kicker над строкой выровнен по тексту. Полоска загрузки/установки **всегда доезжает до 100%** визуально, даже если работа кончилась раньше, и только потом следующий шаг. Скроллбар новостей скрыт; скроллбар чата тоже скрыт (колёсико/жест работают, полоска не рисуется). Имена элементов (`PlayButton`, `ProgressBlock`, `NewsList`, `ChatInput`, …) — контракт C#; менять только USS/иерархию.
- **Main Menu:** та же оболочка, что у лаунчера: шапка / две колонки / графитовые панели 1px. `DefaultPanelSettings` остаётся **1920×1080** (меню не 1280-окно), поэтому USS ≈ **1.5×** лаунчера (`header 111`, кнопки `69`, отступ `33`), чтобы в окне 1280×720 размеры совпали с лаунчером. Левая панель: профиль сверху; внизу вкладки **ЧАТ** / **ИГРЫ** / **ИСТОРИЯ** и квадратная шестерёнка **настроек**. Композер чата — поле и «ОТПРАВИТЬ» в одну строку, как в лаунчере; слева/справа/снизу отступ 8px, как у вкладок ЧАТ/ИГРЫ/ИСТОРИЯ до рамки панели; между вкладками тот же зазор 8px. Выход — крестик ✕ в верхнем правом углу (как лаунчер). Правая колонка — вкладки **ИГРА** / **ДРУЗЬЯ** одинаковой ширины, подписи по центру кнопки; счётчик друзей справа внутри кнопки «ДРУЗЬЯ» (absolute, не сдвигает текст). Вложенные **ДРУЗЬЯ** / **ПРИГЛАШЕНИЯ** тоже равной ширины. Рамка вкладок всегда 1px (неактивная прозрачная), чтобы текст не прыгал при переключении. Хром правой колонки (вкладки **ИГРА** / **ДРУЗЬЯ**, вложенные **ДРУЗЬЯ** / **ПРИГЛАШЕНИЯ**, «В БОЙ» / «ПРИСОЕДИНИТЬСЯ», поле и «ДОБАВИТЬ» у друзей) — тот же шаг **8px**: от рамки панели и между соседними кнопками/вкладками. Квадратики режимов на вкладке ИГРА — в один ряд на всю ширину панели, квадратные (`aspect-ratio: 1`), 8px слева/справа от рамки и 8px между плитками (класс `mm-mode--gap`, не `:last-child`). Под ними досье выбранного режима (`ModeDossier`): крупная схема карты и короткий текст, занимает свободную высоту до «В БОЙ». Схему в `ModeDossierPreview` вставлять сразу (`BuildFillPreview`, класс `mm-mode-dossier__map`) — не ждать измеренный px в `Awake`. Painter2D не рисует при `opacity: 0` интро: после `EnsureVisibleRestState` сбросить и пересобрать карту, плюс `MarkDirtyRepaint` по `GeometryChanged`/`contentRect`. На схеме четыре края (`mm-cam-edge`) — предпочтительная сторона базы на экране (`GameplayCameraPreferences`); схема поворачивается, маркер `mm-cam-home` сидит на выбранном крае. Подсказка — `MatchModeRules.ModeMapNote`. Высоту профиля не задавать из C# — только USS `.mm__profile-badge` (`min-height: 78px`). В высоких `TextField` каретка пустого поля по вертикали совпадает с введённым текстом (`unity-text-element--inner-input-field-component` на 100% высоты). Нижние кнопки ИГРА и ДРУЗЬЯ совпадают по отступу, чтобы низ панели не прыгал. «В БОЙ» создаёт лобби; «вход в лобби» — попап по центру (`LobbyEntryOverlay` как `ui-overlay`), меню не съезжает. «ПРИСОЕДИНИТЬСЯ» подменяет слоты 1:1: поле кода 69px ровно на месте «В БОЙ», «ВОЙТИ» на месте «ПРИСОЕДИНИТЬСЯ», зазор 8px; подсказка «Код комнаты» и «Добавить по имени (Ник#1234)» — `placeholder` внутри пустого поля, не лейбл над ним; ошибка кода и ошибка ника рисуются над полем и вёрстку не сдвигают. Поле ника у «ДОБАВИТЬ» тоже 69px на всю ширину кнопки. Esc возвращает кнопки. Без vignette и без CSS `linear-gradient`. Оверлеи сохраняют существующие `name`.
- **Lobby:** переведён на graphite-палитру (фон `#080908`, панели `#101211`/`#151716`, рамки `#343832`, текст `#DEDBD2`, бронза `#B99A62`). Кнопки 69px, отступы 8px между элементами и от рамок панелей. Слоты игроков — графитовые карточки с рамкой 1px. Скроллбары скрыты (колёсико/жест работают). Handshake-подзаголовок «Версия игры не совпадает…» и блокировка старта остаются. Оверлеи сохраняют существующие `name`.

**Унифицированные экраны** (graphite dark-fantasy): **Main Menu**, **Lobby**, **RacePick**, **Match HUD** (нижний док: minimap chrome, context strip, inspector, командные кнопки, тултипы; top bar, disconnect/results/debug/extra-ability overlays, camera pad buttons — все в палитре), **Pause menu** (панель/кнопки в палитре, равной ширины, 8px отступы), **Match chat composer** (панель в палитре, строки чата остаются крупным золотым текстом с чёрной обводкой для читаемости), **BonusPick overlay** (хром панели, заголовок, таймер, статус, тултип в палитре; слоты уже в палитре после UI-004).

**KEEP (gameplay readability, not chrome):** `match-chat__line` large gold + black outline, gold counter `.match-hud__gold` yellow (resource), bounty popup green, command-feedback orange, targeting tooltip red (combat), world barracks/passive timers white + shadow, unit HP bars, minimap blips/fog.

**BarakiTheme.uss** содержит общие `ui-*` классы и `:root` переменные палитры (фон `--bg-dark: rgb(8, 9, 8)`, панели `--panel-dark / --panel-mid`, рамки `--border-graphite: rgb(52, 56, 50)`, текст `--text-light: rgb(222, 219, 210)`, бронза `--bronze-accent / --bronze-muted`, шаг `--grid-step: 8px`). Переделанные экраны используют переменные вместо хардкоженных цветов старой палитры (cream `#EFE4CF`, rim `#7D7568`). Старые `ui-*` классы с 2px borders и старыми цветами могут ещё использоваться непеределанными экранами (например, Launcher UI), не удалять их без проверки консоли и EditMode тестов. Game-scene UI полностью переведён на graphite; локальные two-class overrides (`ui-dialog.match-hud__pause-panel`, `ui-btn.match-hud__debug-btn`) заменяют BarakiTheme defaults.

## Unity 6.6 USS

Разрешено (graphite, `border-radius: 0`, без «iOS glass»):

- `backdrop-filter: blur(...) tint(...)` — только поверх другого UITK (меню/лобби). **Не** на оверлеях Game-сцены (pause, race-pick, bonus-pick, results): фильтр не семплит 3D и даёт сплошной чёрный. Там обычный `rgba(8, 9, 8, 0.82)`. Не вешать blur на нижний док и миникарту.
- `filter: drop-shadow(...)` — `.ui-dialog` и `.ui-chrome` для глубины панелей.

Запрещено для chrome: кастомные UITK vertex Shader Graph / mesh modifiers (это отдельный VFX-HUD, не тема). Не скруглять панели ради frost.

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
