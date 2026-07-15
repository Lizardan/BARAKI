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
| `/wss` | **same** matchmaker `*.workers.dev` host (stable Worker proxy) |

**`/wss` target (set once — do not paste trycloudflare):**

- Target = matchmaker hostname, e.g. `baraki-matchmaker.lizard268.workers.dev`
- Same value as `WSS_PROXY_TARGET` in `web/activity-shell/config.js` / GitHub var
- Evening quick tunnel registers with matchmaker; Worker proxies Discord WSS → tonight’s tunnel
- Named tunnel (`setup-named-tunnel.ps1`) is optional and needs a Cloudflare domain — not required

6. One-time PC setup:
   - Copy `infra/playtest.env.example` → `infra/playtest.env` and fill `REGISTER_SECRET`
   - Unity: **BARAKI → Build → Windows Dedicated Server (Headless)**
7. Every play evening: double-click `infra/scripts/Start-Playtest.bat` → wait for **READY** → Discord voice → Launch BARAKI.
8. Add the Activity to a **test server** (<25 members pre-verification) → friends Join.

### Smoke checklist (after code/deploy changes)

1. `Start-Playtest.bat` → **READY** (no yellow «paste trycloudflare into Discord»).
2. Discord Launch → Create Match → лобби показывает **КОД** и слоты (не вечное «Подключение…»).
3. Второй игрок Join → оба слота заняты; у первого (слот 0) активна **СТАРТ** после Ready.
4. Выключи tunnel / сервер → Create Match за ~15s показывает ошибку про WSS, без зависания.

Do not commit OAuth secrets or `infra/playtest.env`; use `wrangler secret put REGISTER_SECRET`.
