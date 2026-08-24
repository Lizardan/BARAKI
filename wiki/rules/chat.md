# Chat (Cloudflare menu + match NGO)

## Каналы

| Канал | Где | Транспорт |
|-------|-----|-----------|
| Общий | Launcher / Main Menu вкладка «ОБЩИЙ» | **WebSocket** `wss://…/v1/ws` (push) + HTTP GET для первичной истории |
| Среди друзей | вкладка «СРЕДИ ДРУЗЕЙ» | тот же сокет; fan-out по `session.friendIds` автора |
| ЛС | Friends hub «ЛС» | тот же сокет (`dm`-фреймы); история — GET `/v1/dm/:peerId` |
| Матч | Game HUD (Enter) | `NetworkMatchChat` NGO RPC — **не WS**, мгновенно и без зависимости от Cloudflare |

Инвайты в лобби — Friends `MessageAsync` (UGS), не чат.

**Vivox не используется** (региональные ограничения / Dashboard enable fail).

## WebSocket-транспорт

Один сокет на приложение: `GameChatSocket` (`ClientWebSocket`) → Worker
`GET /v1/ws` → единственный Durable Object `ChatHub` (он же хранит историю и
fan-out'ит). Авторизация — те же заголовки `X-Baraki-*`.

Протокол:
```
→ {"type":"send","channel":"global|friends","text":"…"}
→ {"type":"send","channel":"dm","text":"…","peerId":"…"}
→ {"type":"sync","globalAfter":ts,"friendsAfter":ts,"dm":{peer:ts,…}}
← {"type":"msg","message":{…}}   — чужие и эхо своих (dedup по id на клиенте)
← {"type":"ack","message":{…}}   ← {"type":"error","code":"…"}   ↔ ping/pong
```

Правила (не ломать):
- **WS-only, без fallback-поллинга** (решение пользователя 2026-08-24). Если у
  игрока сокет блокируется прокси — меню-чат недоступен до восстановления;
  матч-чат это не затрагивает. Если плейтест покажет массовые «чат не приходит» —
  вернуть поллинг-страховку отдельным коммитом.
- Reconnect 1→2→4→…→30 c (`GameChatSocketRules.NextReconnectDelaySeconds`);
  после реконнекта клиент автоматически шлёт `sync` по курсорам → ничего не
  теряется между обрывами.
- Отправка идёт через outbox в `GameChatService`: при обрыве сообщения буферизуются
  (лимит 20, drop-oldest) и выстреливаются после восстановления.
- KeepAlive протокола — 30 c; тишина >75 c → принудительный переподключ.
- Сокеты живут в DO в памяти (не персистятся): эвикция DO = кратковременный
  разрыв всех сокетов, клиенты сами переподключаются.

## Bootstrap

[`LauncherController.WarmSocialServicesAsync`](../../Assets/Game/UI/Runtime/Controllers/LauncherController.cs): Авторизация → Профиль → Друзья → **Чат** (`GameChatService.EnsureInitializedAsync`: UGS init → конфигурация → старт сокета → POST `/v1/session` с friendIds) → Ready / ИГРАТЬ.

Soft-fail при недоступном API (timeout/warn).

## Cloudflare

Код: [`Tooling/cloudflare/baraki-chat/`](../../Tooling/cloudflare/baraki-chat/).

```bash
cd Tooling/cloudflare/baraki-chat
npx wrangler deploy
npx wrangler secret put CHAT_API_KEY   # опционально
```

Клиент:

- `GameChatRules.DefaultApiBaseUrl` = `https://baraki-chat.lizard268.workers.dev`
- `GameChatRules.DefaultApiKey` = тот же ключ, что GitHub/Worker secret `CHAT_API_KEY` (вшит в клиент для playtest; PlayerPrefs `baraki.chat.apiBase` / `apiKey` перебивают дефолты)

Деплой: workflow [`.github/workflows/deploy-chat.yml`](../../.github/workflows/deploy-chat.yml) (`workflow_dispatch` или push в `Tooling/cloudflare/baraki-chat/`). Нужны секреты `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `CHAT_API_KEY`.

Идентичность playtest-grade: заголовки `X-Baraki-Player-Id` / `X-Baraki-Player-Name` (UGS). При необходимости позже — JWT verify.

Клиент после `Open` ЛС вызывает `GameChatService.EnsureDirectPeer`, чтобы история/синк подтягивались до первого send.

Debug-консоль: `chat.setkey <key>`, `chat.setbase <url>`, `chat.clear`.

## UI

- [`MenuChatPanel`](../../Assets/Game/UI/Runtime/Views/MenuChatPanel.cs) — вкладки graphite; скроллбар скрыт; после загрузки/новых сообщений прокрутка в конец; при обрыве сокета >10 c подпись канала = «Переподключение…»
- [`FriendsDirectChatPanel`](../../Assets/Game/UI/Runtime/Views/FriendsDirectChatPanel.cs) — `ui-dialog` + mm-chat; скроллбар скрыт; автоскролл вниз
- Матч: текст без подложки, fade ~3.5 с; слой `MatchChatLayer` в UXML **после** нижнего дока, чтобы строки и композер были поверх HUD. Enter открывает поле и сразу ставит каретку (`Focus` + `textSelection`); ввод/отправка через Input System (`Keyboard.current.enterKey`). Строки — крупный золотой текст с чёрной обводкой (`match-chat__line`).

## Лимиты

- Канал: последние 80 сообщений, не старше 36 ч
- ЛС: последние 50, не старше 36 ч
- Cloudflare Worker сам чистит Durable Object (prune + hourly alarm)

## Код

- `Game.Core`: `GameChatRules` (`JsonEscape` — экранирует ВСЕ control-chars), `GameChatSocketRules` (reconnect/outbox/фреймы), `NetworkMatchChatRules`
- `Game.Gameplay`: `GameChatService` (ингест истории/фреймов, outbox, GET-докачка через `HttpClient`; POST больше не используется клиентом), `GameChatSocket` (`ClientWebSocket`-транспорт), `NetworkMatchChat`, `MatchChatNetworkFacade`

## Enter в композерах (не ломать)

KeyDownEvent на TextField регистрировать **только с `TrickleDown.TrickleDown`** — текстовое ядро
UITK съедает Enter/Escape на bubble-up (как с Esc). Затронуто: `MenuChatPanel`,
`FriendsDirectChatPanel`. Матч-чат работает через Input System polling
(`Keyboard.current.enterKey.wasPressedThisFrame`) — другой механизм.

## Задержка доставки

При живом сокете — доли секунды (push). `RefreshNow()` (открытие вкладки/ЛС, отправка)
шлёт `sync`-фрейм; если сокет ещё не поднят — одноразовая HTTP-докачка по курсорам.
