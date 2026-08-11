# Unity Core — C# & MonoBehaviour

## Нейминг

- Классы, методы, свойства, публичные члены: PascalCase (`PlayerController`, `MoveSpeed`)
- Приватные поля: `_camelCase` с `[SerializeField]`, когда нужно в Inspector
- Статические поля: `s_camelCase`; константы: PascalCase (`MaxHealth`)
- Булевы: префикс `_is` / `_has` / `_can`
- События: префикс `On` (`OnPlayerDeath`)
- Async-методы: суффикс `Async`
- Интерфейсы: `I` + PascalCase
- Без Hungarian (`m_`, `f`); без публичных полей без `[SerializeField]`

## MonoBehaviour layout

Порядок членов: сериализованные поля → приватные поля → свойства → lifecycle → методы.

Порядок lifecycle: `Awake` → `OnEnable` → `Start` → `FixedUpdate` → `Update` → `LateUpdate` → `OnDisable` → `OnDestroy`.

## Сериализация

- Inspector-поля: `[SerializeField] private`
- Общая конфигурация: ScriptableObject с read-only свойствами
- Namespace = папка/asmdef (`Game.Core`, `Game.Gameplay`, …)

## Современный C#

- Pattern matching; null-conditional события (`OnDeath?.Invoke()`)
- XML-доки на публичных API
