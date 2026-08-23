# Chat (Cloudflare menu + match NGO)

## Каналы

| Канал | Где | Транспорт |
|-------|-----|-----------|
| Общий | Launcher / Main Menu вкладка «ОБЩИЙ» | Cloudflare Worker `GET/POST /v1/channels/global` |
| Среди друзей | вкладка «СРЕДИ ДРУЗЕЙ» | `/v1/channels/friends` (лента постов друзей) |
| ЛС | Friends hub «ЛС» | `/v1/dm/:peerId` |
| Матч | Game HUD (Enter) | `NetworkMatchChat` NGO RPC |

Инвайты в лобби — Friends `MessageAsync` (UGS), не чат.

**Vivox не используется** (региональные ограничения / Dashboard enable fail).

## Bootstrap

[`LauncherController.WarmSocialServicesAsync`](../../Assets/Game/UI/Runtime/Controllers/LauncherController.cs): Авторизация → Профиль → Друзья → **Чат** (`GameChatService.EnsureInitializedAsync`) → Ready / ИГРАТЬ.

Soft-fail при недоступном API (timeout/warn).

## Cloudflare

Код: [`Tooling/cloudflare/baraki-chat/`](../../Tooling/cloudflare/baraki-chat/).

```bash
cd Tooling/cloudflare/baraki-chat
npx wrangler deploy
npx wrangler secret put CHAT_API_KEY   # опционально
```

Клиент:

- `PlayerPrefs` `baraki.chat.apiBase` = origin Worker (или `GameChatRules.DefaultApiBaseUrl`)
- `PlayerPrefs` `baraki.chat.apiKey` = тот же ключ, что `CHAT_API_KEY` (если задан)

Идентичность playtest-grade: заголовки `X-Baraki-Player-Id` / `X-Baraki-Player-Name` (UGS). При необходимости позже — JWT verify.

Клиент после `Open` ЛС вызывает `GameChatService.EnsureDirectPeer`, чтобы poll подтягивал историю до первого send.

## UI

- [`MenuChatPanel`](../../Assets/Game/UI/Runtime/Views/MenuChatPanel.cs) — вкладки graphite
- [`FriendsDirectChatPanel`](../../Assets/Game/UI/Runtime/Views/FriendsDirectChatPanel.cs) — `ui-dialog` + mm-chat
- Матч: текст без подложки, fade ~3.5 с; композер graphite

## Код

- `Game.Core`: `GameChatRules`, `NetworkMatchChatRules`
- `Game.Gameplay`: `GameChatService` (HTTP poll), `NetworkMatchChat`, `MatchChatNetworkFacade`
