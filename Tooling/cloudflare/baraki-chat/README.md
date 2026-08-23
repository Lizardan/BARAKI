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
| POST | `/v1/session` | body `{ friendIds: string[] }` |
| GET/POST | `/v1/channels/global` | global lobby chat |
| GET/POST | `/v1/channels/friends` | friends feed |
| GET/POST | `/v1/dm/:peerId` | direct messages |

Headers: `X-Baraki-Player-Id`, `X-Baraki-Player-Name`, optional `X-Baraki-Key`.
