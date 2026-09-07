# HacknPlan + UnioTasks — трекинг задач

С **2026-09-03** источник правды по задачам, GDD в PM и roadmap — **HacknPlan**
(проект [242091](https://app.hacknplan.com/p/242091/dashboards/project)).

GitHub Issues **не трогаем**: не edit, не close, не reopen. Они — архив на случай
отката. `GameDesign/*.md` и `wiki/` остаются рабочими копиями на диске для агентов.

`GameDesign/TODO.md` — карта фаз и лог решений, не бэклог.

## Где что лежит

| Вопрос | Где |
|--------|-----|
| Что делать сейчас | открытые work items HacknPlan (board **Now**) = панель UnioTasks |
| Дизайн / GDD в PM | Game Design Model в том же проекте |
| В каком порядке фазы | milestones: PRE-RACE2 → GATE → Early Access |
| Зачем так устроено | `GameDesign/` + `wiki/rules/` (локальные копии) |
| История GitHub | закрытые issues, только чтение |

Один открытый work item = одна работа, которую можно начать и закрыть.
User story (`isStory`) — только если кусок реально дробится.

## Язык

| Часть карточки | Язык |
|----------------|------|
| Заголовок | **Русский** (+ ID-префикс, если есть) |
| Суть / описание для человека | **Русский** |
| Технический контекст, пути, acceptance для агента | **English** — секция `## Agent context (EN)` |

Код, команды, пути — как есть.

## Структура work item

```markdown
[ID] Русский заголовок

## Суть
## Правила/Ограничения
## Acceptance criteria   — чекбоксы - [ ]
## Agent context (EN)
```

Нативные сущности HacknPlan (не метки GitHub):

- **Stage:** Planned → In progress → Testing → Completed
- **Importance:** Urgent / High / Normal / Low
- **Category:** Programming, Design, Bug, UI (если есть), …
- **Board:** Now (текущее окно GATE) или backlog (`boardId: 0`)
- **Tags:** `github` (импорт), `deferred`, `needs-triage`, `bug`
- **Dependencies** — блокер (`isBlocked`), не текстовый список
- **Design element** — привязка к GDM
- **Subtasks** — вместо чекбоксов в теле, если удобно

## UnioTasks (Cursor)

Расширение живёт **только** как Cursor extension, не в git BARAKI.
Список = work items, у которых stage ≠ Completed.

| Группа панели | HacknPlan |
|---------------|-----------|
| Выполняется | stage In progress |
| Ждут апрува | stage Testing |
| Горит | importance Urgent |
| Высокий | High |
| Очередь | Planned + Normal |
| Низкий | Low |
| Заблокировано | `isBlocked` |
| Отложено | tag `deferred` |

Кнопки:

- ▶ — если не blocked: stage In progress + промпт агента. Не закрывает карточку через API.
- 🚀 — resume сессии, stage не меняет
- ✔ — Completed, если subtasks закрыты (или подтверждение); иначе Testing
- ❌ — Completed после подтверждения
- DnD — PATCH stage / importance / deferred
- Завести — `POST /workitems`, не GitHub

Агент **не** закрывает work item и **не** трогает GitHub Issues.

## Обязанности агента (opencode) по stage

Кодагент обязан вести stage карточки **сам**, не дожидаясь напоминания:

- **Взял задачу в работу** → `PATCH stageId: 2` (In progress).
- **Завершил работу** (тесты green, доки записаны) → `PATCH stageId: 3` (Testing) +
  комментарий с итогом (что сделано, тесты, пути, открытые вопросы).
- **Completed (stageId 4)** — только пользователь (UnioTasks ✔/❌ после апрува).

Инструменты: `Tooling/HacknPlan/client.js` (`HacknPlanClient.fromEnv()`, ключ в
`Tooling/HacknPlan/.secrets.json`). Поиск карточки — `listAllWorkItems()` по префиксу
в заголовке (`[FACELESS-007]`), смена stage — `patchWorkItem(id, { stageId })`,
итог — `addComment(id, "## Agent summary ...")`. Rate limit — клиент сам держит
очередь/ретраи.

## Коммиты

По корневому `AGENTS.md`. Если нужен след в HacknPlan — префикс `hnp-{id}` в
сообщении коммита (нативная интеграция HacknPlan ↔ git). GitHub issue не
редактировать.

## Что не кладём в трекер

Логи консоли, дампы playtest, разовые вопросы без задачи. Баги — category Bug
и/или tag `bug`.
