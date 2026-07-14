# Discord Application setup (from scratch)

Follow this once before a friend playtest. Full runbook: [FREE0.md](FREE0.md). Evening one-click: `infra/scripts/Start-Playtest.bat`.

1. Open [Discord Developer Portal](https://discord.com/developers/applications) → **New Application** → `BARAKI`.
2. Copy **Application ID** into GitHub variable `DISCORD_CLIENT_ID` (injected into `web/activity-shell/config.js` on deploy).
3. **OAuth2**:
   - Public Client = ON
   - Redirect: `https://<APPLICATION_ID>.discordsays.com`
   - Scopes used by Activity: `identify`, `guilds`
4. **Activities** → enable Embedded App / Activity (Web).
5. Deploy Pages + Workers, then **URL Mappings** (stable — do not change every night):

| Prefix | Target |
|--------|--------|
| `/` | your `*.pages.dev` host |
| `/api` | your matchmaker `*.workers.dev` host |
| `/wss` | **named tunnel** hostname from `setup-named-tunnel.ps1` |

6. One-time PC setup:
   - Copy `infra/playtest.env.example` → `infra/playtest.env` and fill `REGISTER_SECRET`
   - Run `.\infra\scripts\setup-named-tunnel.ps1`
   - Set `WSS_PROXY_TARGET` in `config.js` (or env for CI) to the same host as `/wss`
   - Unity: **BARAKI → Build → Windows Dedicated Server (Headless)**
7. Every play evening: double-click `infra/scripts/Start-Playtest.bat` → Discord voice → Launch BARAKI.
8. Add the Activity to a **test server** (<25 members pre-verification) → friends Join.

Do not commit OAuth secrets or `infra/playtest.env`; use `wrangler secret put REGISTER_SECRET`.
