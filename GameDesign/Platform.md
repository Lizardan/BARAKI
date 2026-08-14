---
doc_id: platform
version: 1.1
status: locked
depends_on: [vision, technical, match_flow]
provides: [windows_hub, ugs_lobby_relay, friends_cloudsave, distribution_github_releases, host_migration]
---

# Platform

> **Primary ship:** Windows x64 Standalone. Один Unity-проект.

## Целевой UX

```
Запуск BARAKI.exe
  → Main Menu (info hub): профиль, друзья, обновления
  → Create / Join / Invite → Lobby (Unity Lobby + Relay)
  → Хост = listen-server (NGO StartAsHost); остальные = clients
  → Countdown → матч
```

## Техническая модель

```entity
id: NET_PRODUCTION
model: host_as_server
clients: Windows_Standalone
host: listen_server_plus_local_client
package: Netcode_for_GameObjects
discovery: Unity_Lobby
nat: Unity_Relay
mvp: true
```

```entity
id: UGS_STACK
authentication: Anonymous_then_optional_platform
lobby: Unity_Lobby
relay: Unity_Relay
friends: Unity_Friends
profile: Cloud_Save
mvp_online: Auth_Lobby_Relay
mvp_social: Friends_CloudSave
```

### Host-as-server

Windows native client = listen-server. Один процесс хоста симулирует матч и рендерит локального клиента.

```entity
id: HOST_MIGRATION
trigger: host_process_exit_or_disconnect
flow: pause_all → elect_new_host → relay_rebind → full_state_transfer → unpause
reconnect: session_token_rejoin
mvp: true
```

## Main Menu hub

| Блок | Источник |
|------|----------|
| Ник / ранг / очки | Cloud Save (`displayName`, `rank`, `points` stubs) |
| Друзья online / in game | Friends presence |
| Create / Join | Lobby join code + Relay |
| Invite | Friends → private Lobby |
| Update gate | GitHub Release tag (`/releases/latest` → tag); Play disabled если outdated |
| Legal | GitHub Pages — https://lizardan.github.io/BARAKI/privacy.html · terms.html |

## Distribution

```entity
id: DIST_PIPELINE
build: GitHub_Actions_on_push_main
artifact: BARAKI-vX.Y.Z.zip
store: GitHub_Releases
version_source: release_tag
versioning: auto_semver_patch_on_push
client: force_update_via_ApplyUpdate_bat
mvp: true
```

Канал (публичный репо):

1. `git push` в `main` (изменения в Assets/Packages/ProjectSettings/…)
2. Actions сам делает `patch` bump (`v0.2.0` → `v0.2.1`), собирает Windows, создаёт Release. Смена линии: `PlayerSettings.bundleVersion` = `X.Y.1` (Editor всегда GitHub+1) → следующий релиз `vX.Y.0`.
3. Клиент: `GET …/releases/latest` (redirect → tag) → сравнить с `Application.version` → скачать `BARAKI-{tag}.zip`
4. Ручной major/minor: Actions → **Deploy Windows** → bump = minor/major
5. Пропуск релиза: commit message содержит `[skip release]`

SHA256: релиз публикует ассет `BARAKI-{tag}.zip.sha256` и хеш в notes. Клиент проверяет его, только когда `BARAKI_UPDATE_URL` указывает на манифест `version.json` с полем `sha256` (default `/releases/latest`-флоу хеш не проверяет).

Prune: workflow держит последние 2 полноценных релиза `v*`.

## Non-goals

- Отдельный launcher exe
- Cloudflare R2 (требует карту на аккаунте)

## Locked decisions

| Решение | Значение |
|---------|----------|
| Ship client | **Windows x64 Standalone** |
| Net model | **Host-as-server** + Lobby + Relay |
| Social | **UGS Friends + Cloud Save** |
| Builds | **GHA → GitHub Releases → in-game force update** |
| Host migration / reconnect | **Required** (listen-host peer model) |
