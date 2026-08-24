# BARAKI menu chat (Cloudflare Worker)

## Deploy

```bash
cd Tooling/cloudflare/baraki-chat
npx wrangler deploy
npx wrangler secret put CHAT_API_KEY   # optional but recommended
```

Workers URL example: `https://baraki-chat.<account>.workers.dev`

Set the same values in the Unity client (`GameChatRules` / PlayerPrefs):

- `baraki.chat.apiBase` = worker origin
- `baraki.chat.apiKey` = same as `CHAT_API_KEY` (if set)

## API

| Method | Path | Notes |
|--------|------|-------|
| GET | `/v1/ws` | WebSocket push: `send` / `sync` frames, `msg` / `ack` / `error` back |
| POST | `/v1/session` | body `{ friendIds: string[] }` |
| GET | `/v1/channels/global` | global history (`?after=<ts>` catch-up) |
| GET | `/v1/channels/friends` | friends feed history |
| GET | `/v1/dm/:peerId` | DM history |

Message sending happens over the WebSocket (`send` frame); HTTP is read-only
history + the session sync.

Headers: `X-Baraki-Player-Id`, `X-Baraki-Player-Name`, optional `X-Baraki-Key`.

## Retention

Worker Durable Object stores history in SQLite-backed storage and **deletes old messages**:

- global / friends feed: last **80** messages, drop older than **36 hours**
- DM: last **50** messages, same age cap
- hourly `alarm()` prune so storage cannot grow without bound
- idle sessions older than 7 days are dropped

Unity client (`GameChatRules.MaxChannelHistory` / `MaxDirectHistory` / `HistoryRetentionHours`) also trims local buffers and ignores expired payloads on ingest.
