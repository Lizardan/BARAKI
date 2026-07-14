# CI/CD — что автоматизировано и что руками

## WebGL CI: exit 134 / ADTM

Лицензия уже ок. Exit **134** на GitHub-hosted runner = Unity abort (часто OOM на Unity 6 + URP/VFX/ECS).

1. Запушь обновлённый `deploy-activity.yml` + `WebGLCiBuild.cs` и перезапусти **Deploy Activity**.
2. Если снова 134 — обходной путь:
   - Собери WebGL локально → положи в `web/activity-shell/Build/`
   - Запусти workflow **Deploy Activity Shell (prebuilt WebGL)**
3. Долгосрочно: self-hosted runner на PC.

---

Связанные workflows:
- [`.github/workflows/deploy-activity.yml`](../.github/workflows/deploy-activity.yml) — Unity WebGL → Cloudflare Pages
- [`.github/workflows/deploy-matchmaker.yml`](../.github/workflows/deploy-matchmaker.yml) — Workers matchmaker
- Вечерний helper: [`scripts/playtest-evening.ps1`](scripts/playtest-evening.ps1)

**Важно:** `cloudflared` tunnel к **game server на твоём PC** нельзя полностью убрать из FREE-0 — сервер крутится у тебя дома. CI деплоит только **клиент (WebGL)** и **matchmaker**. Game server + tunnel остаются one-click скриптом на вечер.

---

## Один раз руками (setup)

### 1. Cloudflare

1. Аккаунт на [dash.cloudflare.com](https://dash.cloudflare.com)
2. **Workers & Pages** → Create Pages project `baraki-activity` (можно пустой; первый CI deploy создаст)
3. API Token: *Edit Cloudflare Workers* + *Account Cloudflare Pages Edit*  
   → сохрани как GitHub secret `CLOUDFLARE_API_TOKEN`
4. Account ID (справа в overview) → secret `CLOUDFLARE_ACCOUNT_ID`
5. KV для matchmaker: **не нужен** (FREE-0 хранит tunnel/match in-memory).
6. Секрет регистрации tunnel:
   ```powershell
   cd infra/workers/matchmaker
   npx wrangler login
   npx wrangler secret put REGISTER_SECRET
   ```
   Тот же пароль → GitHub secret `MATCHMAKER_REGISTER_SECRET` (опционально, для локальных скриптов)

### 2. Unity license для CI (game-ci)

1. Personal: активируй лицензию, сохрани `.ulf` содержимое → secret `UNITY_LICENSE`  
   Либо `UNITY_EMAIL` + `UNITY_PASSWORD` (+ `UNITY_SERIAL` для Plus/Pro)
2. Docs: https://game.ci/docs/github/getting-started

### 3. Discord Application

См. [`DISCORD_SETUP.md`](DISCORD_SETUP.md).  
Application ID → GitHub **variable** `DISCORD_CLIENT_ID` (не secret — он публичный в клиенте).

### 4. GitHub repo secrets / vars

| Name | Type | Для |
|------|------|-----|
| `CLOUDFLARE_API_TOKEN` | secret | Pages + Workers |
| `CLOUDFLARE_ACCOUNT_ID` | secret | Pages + Workers |
| `UNITY_LICENSE` (или EMAIL/PASSWORD/SERIAL) | secret | WebGL build |
| `MATCHMAKER_REGISTER_SECRET` | secret | Worker secret sync |
| `DISCORD_CLIENT_ID` | variable | inject в config.js |
| `WSS_PROXY_TARGET` | variable | optional: stable tunnel host for Discord WSS proxy |
| `PAGES_PROJECT` | variable | optional, default `baraki-activity` |

### 5. Discord URL Mappings (после первого успешного deploy)

| Prefix | Target |
|--------|--------|
| `/` | `baraki-activity.pages.dev` (точный host из CF) |
| `/api` | `baraki-matchmaker.<subdomain>.workers.dev` |
| `/wss` | tunnel host вечера (или обновляй named tunnel) |

---

## Каждый push (автоматически)

На `main`/`master`:

| Изменения | CI |
|-----------|-----|
| `Assets/`, `Packages/`, `web/activity-shell/` | WebGL build + Pages deploy |
| `infra/workers/matchmaker/` | Workers deploy |

Можно вручную: Actions → **Deploy Activity** / **Deploy Matchmaker** → Run workflow.

---

## Каждый игровой вечер (руками, ~1 клик)

1. Собери dedicated server локально (редко, после изменений геймплея):
   Unity → **BARAKI → Build → Windows Dedicated Server (Headless)**
2. Один раз настройте named tunnel + `infra/playtest.env`:
   `.\infra\scripts\setup-named-tunnel.ps1`
   Discord Portal `/wss` → тот же hostname **один раз**.
3. Каждый вечер: двойной клик
   [`infra/scripts/Start-Playtest.bat`](scripts/Start-Playtest.bat)
   (читает `infra/playtest.env`, поднимает server + named tunnel + register).
4. Окно не закрывай.
5. Discord desktop → голосовой канал → Launch **BARAKI** → друзья Join.

Named tunnel hostname **не меняется** — Discord `/wss` больше не править каждый вечер.
`infra/playtest.env` в `.gitignore` (секреты локально).

---

## Что НЕ автоматизировано (и почему)

| Вещь | Почему |
|------|--------|
| Headless game server 24/7 | FREE-0 = твой PC; 24/7 = FREE-1 Oracle |
| `cloudflared` к localhost | Нужен живой процесс на хосте |
| Discord Developer Portal клики | OAuth / Activity / mappings — разово руками |
| Unity license renewal | Истекает — обновить secret |
| WebGL размер >25 MB/файл | Может упасть Pages; сжимать билд / дробить ассеты |

---

## Локальный deploy без CI (если надо)

```powershell
# Pages (уже собранный WebGL лежит в web/activity-shell/Build)
.\infra\scripts\deploy-pages.ps1

# Worker
cd infra\workers\matchmaker
npm install
npx wrangler deploy
```
