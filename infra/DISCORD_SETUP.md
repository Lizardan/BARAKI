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

**`/wss` target (required for Discord evenings):**

- Run `.\infra\scripts\setup-named-tunnel.ps1` once → set the **same** hostname as `WSS_HOST` in `playtest.env` and as GitHub var `WSS_PROXY_TARGET` (injected into `config.js`).
- Portal `/wss` stays fixed. Do **not** rely on quick tunnel for friend playtests.
- Dev escape hatch only: `ALLOW_QUICK_TUNNEL=1` with empty `WSS_HOST` (paste new `*.trycloudflare.com` into Portal every evening).

6. One-time PC setup:
   - Copy `infra/playtest.env.example` → `infra/playtest.env` and fill `REGISTER_SECRET` + `WSS_HOST`
   - Run `.\infra\scripts\setup-named-tunnel.ps1`
   - Unity: **BARAKI → Build → Windows Dedicated Server (Headless)**
7. Every play evening: double-click `infra/scripts/Start-Playtest.bat` → wait for **READY** → Discord voice → Launch BARAKI.
8. Add the Activity to a **test server** (<25 members pre-verification) → friends Join.

### Smoke checklist (after code/deploy changes)

1. `Start-Playtest.bat` → **READY** with named tunnel (no yellow «paste /wss»).
2. Discord Launch → Create Match → лобби показывает **КОД** и слоты (не вечное «Подключение…»).
3. Второй игрок Join → оба слота заняты; у первого (слот 0) активна **СТАРТ** после Ready.
4. Выключи tunnel / сервер → Create Match за ~15s показывает ошибку про WSS, без зависания.

Do not commit OAuth secrets or `infra/playtest.env`; use `wrangler secret put REGISTER_SECRET`.
