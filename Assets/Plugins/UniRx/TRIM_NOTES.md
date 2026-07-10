# UniRx trim notes (UI Toolkit template)

This vendored copy is trimmed for **UI Toolkit-only** projects (no `com.unity.ugui`).

## Removed on trim

- `Examples/` — sample scripts
- `Scripts/UnityEngineBridge/UnityUIComponentExtensions.cs`
- `Scripts/UnityEngineBridge/UnityGraphicExtensions.cs`
- EventSystems trigger scripts (17 files): `ObservableBeginDragTrigger`, `ObservablePointerClickTrigger`, etc.
- uGUI regions in `ReactiveCommand.cs` and `ObservableTriggerExtensions.Component.cs`

## After Asset Store update

1. Re-apply deletions above (or diff against this list).
2. Ensure `Scripts/UniRx.asmdef` has `"references": []` (no `Unity.ugui`).
3. UI bindings live in `Assets/Game/UI/Runtime/Extensions/` — not in this plugin.

## Kept

- Core Observable / ReactiveProperty / ReactiveCommand
- MainThreadDispatcher, lifecycle triggers (Update, Destroy, Collision, etc.)
- `UniRx.Toolkit.ObjectPool`
