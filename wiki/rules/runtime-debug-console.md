# Runtime debug console

IMGUI-консоль (`Game.Core.RuntimeDebugConsole`), bootstrap `BeforeSceneLoad`, `DontDestroyOnLoad`.

## Хоткеи

| Клавиша | Действие |
|---------|----------|
| `` ` `` / `~` (`Keyboard.backquoteKey`, физ. клавиша слева от `1`) | Открыть (только когда закрыта) |
| `Esc` | Закрыть |
| `Enter` | Выполнить команду |

Backspace **не** открывает консоль — иначе ломает правку `TextField` в UI Toolkit.
