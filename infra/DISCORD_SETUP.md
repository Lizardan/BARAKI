# Discord Application setup (from scratch)

Follow this once before a friend playtest. Full runbook: [FREE0.md](FREE0.md). Evening one-click: `infra/scripts/Start-Playtest.bat`.

1. Open [Discord Developer Portal](https://discord.com/developers/applications) → **New Application** → `BARAKI`.
2. Copy **Application ID** into GitHub variable `DISCORD_CLIENT_ID` (injected into `web/activity-shell/config.js` on deploy).
3. **OAuth2**:
   - Public Client = ON
   - Redirect: `https://<APPLICATION_ID>.discordsays.com`
   - Scopes used by Activity: `identify`, `guilds`
4. **Activities** → enable Embedded App / Activity (Web).
5. Deploy Pages + Workers, then **URL Mappings**:

| Prefix | Target |
|--------|--------|
| `/` | your `*.pages.dev` host |
| `/api` | your matchmaker `*.workers.dev` host |
| `/wss` | tunnel hostname (see below) |

**`/wss` target:**

- **Named tunnel** (stable): hostname from `setup-named-tunnel.ps1` → set the same value as `WSS_HOST` in `playtest.env`. Mapping stays fixed.
- **Quick tunnel** (`WSS_HOST` empty): each evening `Start-Playtest.bat` prints a new `*.trycloudflare.com` hostname (also copied to clipboard). Paste it into Discord `/wss` before Launch.

6. One-time PC setup:
   - Copy `infra/playtest.env.example` → `infra/playtest.env` and fill `REGISTER_SECRET`
   - Optional: run `.\infra\scripts\setup-named-tunnel.ps1` and set `WSS_HOST` for a stable `/wss`
   - Unity: **BARAKI → Build → Windows Dedicated Server (Headless)**
7. Every play evening: double-click `infra/scripts/Start-Playtest.bat` → wait for **READY** → (if quick tunnel) update Discord `/wss` → Discord voice → Launch BARAKI.
8. Add the Activity to a **test server** (<25 members pre-verification) → friends Join.

Do not commit OAuth secrets or `infra/playtest.env`; use `wrangler secret put REGISTER_SECRET`.
