# Реактивность (UniRx)

Пакет: vendored `Assets/Plugins/UniRx/` (trimmed, без uGUI). UI-биндинги: `UI/Runtime/Extensions/UniRx/`.

## Disposal

- UI: **`UIBindingScope.Add(IDisposable)`** — обязательно для всех UITK-биндингов
- Gameplay MonoBehaviour: `.AddTo(this)` или `CompositeDisposable` в `OnDestroy`
- ViewModel: dispose `ReactiveProperty` / `ReactiveCommand`, когда экран уничтожается и не переиспользуется

## UI-биндинги (UI Toolkit only)

```csharp
using UniRx;
using UnityEngine.UIElements;

titleLabel.SubscribeToText(viewModel.Title);
startButton.BindTo(viewModel.StartCommand);
panel.OnPointerEnterAsObservable().Subscribe(...).AddTo(scope); // scope через UIBindingScope.Add
```

**Запрещено:** `UnityEngine.UI.*`, vendored uGUI UniRx-экстеншены (удалены из плагина).

## Observable vs EventChannel

| Использовать | Когда |
|--------------|-------|
| `ReactiveProperty` / `Observable` | ViewModel, потоки, таймеры, UI-биндинг |
| ScriptableObject EventChannel | Кросс-сценные события, дизайнерские события (когда добавятся) |

## MainThreadDispatcher

Создаётся автоматически в runtime при первом использовании планировщика UniRx. Префаб в сцене не нужен.

## Будущее

UniRx — legacy (2019). Миграция на R3 — отдельная задача; стандарт проекта — UniRx + UITK-мост.
