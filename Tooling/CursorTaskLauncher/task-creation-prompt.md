Создай задачу в GitHub Issues текущего репозитория.

Приоритет: {priority}

Описание:
{user_text}

---

Определи из описания:
- Префикс ID (PRE, GATE, EA, CHAT, BUG, FEATURE и т.д.)
- Дополнительные метки если применимо (bug, feature, enhancement)

Создай issue:

gh issue create \
  --title "[PREFIX] Название на русском" \
  --label "todo-task,ПРИОРИТЕТ_МЕТКА" \
  --body "ФОРМАТ_НИЖЕ"

Маппинг приоритета в метку:
- 🔥 Горит → critical
- 🎯 Высокий → high
- 📋 По очереди → (без доп. метки)
- ⬇️ Низкий → low
- 🔒 Заблокировано → blocked
- 🧊 Морозилка → deferred

Тело issue:

## Суть
{что сделать и зачем, по-русски}

## Acceptance criteria
- [ ] Критерий 1
- [ ] Критерий N

## Agent context (EN)
{техконтекст: файлы, классы, зависимости}

## Refs / links
{wiki/rules, GDD, связанные issues}

НЕ ЗАКРЫВАЙ issue.
