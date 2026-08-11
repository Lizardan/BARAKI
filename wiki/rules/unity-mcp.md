# Unity MCP (opencode → Unity Editor)

Мост `unityMCP` на `:6400`. Пути — относительно `Assets/`.

## Политика

- Editor-операции — **MCP-first**: объекты сцены/префабы через `manage_gameobject` / `manage_components`,
  ассеты — `manage_asset`, сцены — `manage_scene`. Не править `.unity` / `.prefab` YAML руками
- Перед мутацией редактора читать `mcpforunity://editor/state`, ждать `isCompiling: false`
- После создания/правки скриптов — `read_console` на ошибки компиляции
- Unity API проверять через `unity_reflect` → `unity_docs`, не полагаться на память модели;
  шейдеры/материалы/спрайты искать в ассетах (`manage_asset` search), не выдумывать

## Воркфлоу

- Множественные однотипные операции — `batch_execute` (create / add-компоненты / свойства)
- Чтение сцен/иерархий — paging: `get_hierarchy` page_size ~50, следовать `next_cursor`;
  `get_components` начинать `include_properties=false`
- Тесты: `run_tests` / `get_test_job` (группа **testing**); после правки кода прогонять затронутые
- Группы инструментов активировать по задаче: **ui** (UXML/USS/UIDocument), **docs**, **testing**; **core** — по умолчанию

## Зависимости

Единственный MCP-optional-dep — Cinemachine. VFX Graph / ProBuilder / glTFast / Roslyn **не установлены**:
`manage_vfx` (кроме ParticleSystem), `import_model` (glTF) и `execute_code` C# 12 ограничены.
