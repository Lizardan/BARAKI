# Асинхронность (Unity)

## Дерево решений

1. `Game.Core` (bootstrap) → `Awaitable` (`NextFrameAsync`, `LoadSceneAsync`)
2. Gameplay / UI → `async UniTask` / `UniTaskVoid`
3. **Никогда** `Task` или голый `async void` (кроме message-заглушек Unity)

## UniTask

- Отмена: `this.GetCancellationTokenOnDestroy()` на host-`MonoBehaviour`
- Fire-and-forget: `.Forget()` только когда отмена обрабатывается внутри метода
- Клики UI: `await button.OnClickAsync(cancellationToken)` — `UnityAsyncExtensions.UIToolkit`
- Загрузка сцены: `await SceneManager.LoadSceneAsync(name).ToUniTask(cancellationToken: ct)`

## UniRx + async

- Клики: `ReactiveCommand` + `BindTo(Button)`
- Длинные async-цепочки: `UniTask` в Controller, состояние обновлять через ViewModel

## Опциональные интеграции

- Addressables / DOTween: включить опциональные UniTask-asmdef из пакета, когда пакет добавлен
  (сейчас **не установлены**)
