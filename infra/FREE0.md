# BARAKI FREE-0 Runbook

Playtest stack: **PC headless game server + Cloudflare Tunnel + Pages/Workers + Discord Activity**.

WebGL hosting: **Cloudflare Pages** (not GitHub Pages).

**CI/CD:** see [`CI.md`](CI.md) — WebGL + matchmaker деплоятся с GitHub Actions; вечерний tunnel — скрипт на PC.

**Руками по шагам:** раздел «Один раз» + «Каждый вечер» в [`CI.md`](CI.md). Discord portal: [`DISCORD_SETUP.md`](DISCORD_SETUP.md).

---

## Phase B — WSS smoke (no Discord iframe)

### Prerequisites

- Unity 6.5 project opens
- `cloudflared` installed ([download](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/))
- Menu: **BARAKI → Build → Windows Dedicated Server (Headless)** → `Builds/WindowsServer/`

### 1. Start dedicated server (host PC)

```powershell
cd "F:\Unity Projects\BARAKI\Builds\WindowsServer"
$env:BARAKI_SERVER = "1"
.\BARAKI.exe -batchmode -nographics -barakiServer -listenPort 7777 -players 2
```

Or from Editor player build with the same args. Server listens WebSockets on `0.0.0.0:7777`.

### 2. Expose with Cloudflare Tunnel

```powershell
cloudflared tunnel --url http://127.0.0.1:7777
```

Copy the printed hostname, e.g. `https://random-words.trycloudflare.com`.  
For Unity WebSocket clients use:

```text
ws://random-words.trycloudflare.com
```

or (if tunnel terminates TLS for browsers / Discord):

```text
wss://random-words.trycloudflare.com
```

> Quick Tunnel URLs change every restart. Named tunnels are better for Discord evenings.

### 3. Two clients (Editor / WebGL)

**Client A (host UX, NetDev):**

1. Before Play, enable NetDev (temporary): call `MatchSessionService.UseNetDev()` from a bootstrap, **or** set env `BARAKI_NETDEV=1` and launch the player (see `MatchSessionBootstrap`).
2. Main Menu → Create → N=2 → Lobby → Ready → Start.

**Client B (join):**

1. Same NetDev / Discord backend.
2. Join with room code **or** connect with endpoint override:
   - Editor: set transport to the tunnel host via session join returning `ws://…` from matchmaker (Phase A), **or** for local B smoke both on LAN use `ws://127.0.0.1:7777` with `UseNetDev()`.

**Same-machine B smoke (simplest):**

1. Terminal 1: dedicated server `-listenPort 7777`
2. Terminal 2: Editor Play as client — Main Menu Join after `UseNetDev()` (endpoint `ws://127.0.0.1:7777`)
3. Terminal 3: second Editor/ParrelSync or second player build — Join

Expect: lobby slots fill via `NetworkLobbyState`, Start loads Game, clients receive snapshots.

### Phase B success criteria

- [ ] Server listens without UI
- [ ] Two clients connect over WebSockets
- [ ] Ready / Start replicated
- [ ] Client sees unit ghost markers from snapshots

---

## Phase A — Discord Activity (from scratch)

### A1. Discord Developer Portal checklist

1. [https://discord.com/developers/applications](https://discord.com/developers/applications) → **New Application** → name `BARAKI`
2. **OAuth2** → copy **Client ID** / secret (Workers secret later)
3. **Activities** → Enable Activity / Embedded App
4. OAuth2 scopes: `identify`, `guilds`, `applications.commands` (per current Discord Activity docs)
5. Create / use a **test guild** (<25 members before Activity verification)
6. After Pages + Workers deploy, set **URL Mappings**:

| Prefix | Target (no `https://`) |
|--------|-------------------------|
| `/` | `baraki-xxxx.pages.dev` |
| `/api` | `baraki-matchmaker.xxx.workers.dev` |
| `/wss` | `your-tunnel-host.trycloudflare.com` (or named tunnel) |

7. Invite bot / enable Activity in a voice channel → Launch / Join

### A2. Workers matchmaker

```powershell
cd infra/workers/matchmaker
npm install
npx wrangler secret put REGISTER_SECRET   # same value you POST with
npx wrangler deploy
```

Host registers tunnel URL once per evening:

```powershell
curl -X POST "https://<worker>/api/v1/admin/register-tunnel" `
  -H "Authorization: Bearer $REGISTER_SECRET" `
  -H "Content-Type: application/json" `
  -d "{\"wss_url\":\"wss://your-tunnel-host.trycloudflare.com\"}"
```

Clients call:

- `POST /api/v1/match/ensure` `{ instance_id, player_count, discord_user_id }`
- `GET /api/v1/match/{instance_id}`

### A3. Activity shell + WebGL → Cloudflare Pages

1. Build Unity **WebGL** into `web/activity-shell/Build/`
2. Fill `web/activity-shell/config.js` (`DISCORD_CLIENT_ID`, Pages/Workers hosts)
3. Deploy:

```powershell
cd web/activity-shell
npx wrangler pages deploy . --project-name baraki-activity
```

### A4. Friend evening sequence

1. Build + start dedicated server (`-players 2` or `4`)
2. `cloudflared tunnel --url http://127.0.0.1:7777`
3. `register-tunnel` with `wss://…`
4. Discord voice → Launch BARAKI Activity
5. Friends Join → lobby Ready → Start → play

### A5. Troubleshooting

| Symptom | Check |
|---------|--------|
| CSP / blocked fetch | URL Mappings + `patchUrlMappings` in shell |
| WebGL file too large for Pages | Compress build; split StreamingAssets; 25 MB/file limit |
| Clients connect, no lobby | Server spawned `NetworkLobbyState`? Check server log |
| Works on LAN, fails in Discord | Missing `/wss` mapping or using `ws://` instead of `wss://` |

---

## Env / CLI quick reference

| Flag / env | Meaning |
|------------|---------|
| `-barakiServer` / `BARAKI_SERVER=1` | Dedicated server entry |
| `-port` / `PORT` | Listen port (default 7777) |
| `-players` / `PLAYER_COUNT` | N slots |
| `BARAKI_NETDEV=1` | Use NetDev session backend (ws localhost) |
| `BARAKI_MATCHMAKER_URL` | Override matchmaker base URL for Discord backend |
