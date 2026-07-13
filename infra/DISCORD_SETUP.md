# Discord Application setup (from scratch)

Follow this once before a friend playtest. Full runbook: [FREE0.md](FREE0.md).

1. Open [Discord Developer Portal](https://discord.com/developers/applications) → **New Application** → `BARAKI`.
2. Copy **Application ID** into `web/activity-shell/config.js` → `DISCORD_CLIENT_ID`.
3. **Activities** → enable Embedded App / Activity.
4. **OAuth2** → scopes: `identify`, `guilds` (and others required by current Discord Activity docs).
5. Deploy Pages + Workers (see FREE0.md), then **URL Mappings**:

| Prefix | Target |
|--------|--------|
| `/` | your `*.pages.dev` host |
| `/api` | your matchmaker `*.workers.dev` host |
| `/wss` | Cloudflare Tunnel host for the game server |

6. Add the Activity to a **test server** (guild under 25 members is fine pre-verification).
7. Voice channel → Launch Activity → friends Join.

Do not commit OAuth secrets; use `wrangler secret put` for `REGISTER_SECRET`.
