---
doc_id: platform
version: 1.0
status: locked
depends_on: [vision, technical, match_flow]
provides: [windows_hub, ugs_lobby_relay, friends_cloudsave, distribution_github_releases, host_migration]
---

# Platform

> **Primary ship:** Windows x64 Standalone. Один Unity-проект. Discord Activity / WebGL — **non-goals**.

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

### Почему host-as-server

Windows native client может быть listen-server. Dedicated server per match **не** нужен для friends play / MVP.

```entity
id: HOST_MIGRATION
trigger: host_process_exit_or_disconnect
flow: pause_all → elect_new_host → relay_rebind → full_state_transfer → unpause
reconnect: session_token_rejoin
mvp: false
post_mvp: true
```

**MVP:** хост вылетел → матч завершается. Reconnect нет.

**Post-MVP:** полный перенос состояния + reconnect вылетевшего клиента.

## Main Menu hub

| Блок | Источник |
|------|----------|
| Ник / ранг / очки | Cloud Save (`displayName`, `rank`, `points` stubs) |
| Друзья online / in game | Friends presence |
| Create / Join | Lobby join code + Relay |
| Invite | Friends → private Lobby |
| Update gate | GitHub Releases `version.json`; Play disabled если outdated |
| Legal | GitHub Pages — https://lizardan.github.io/BARAKI/privacy.html · terms.html |

## Distribution

```entity
id: DIST_PIPELINE
build: GitHub_Actions_on_push_main
artifact: windows_x64_zip
store: GitHub_Releases
manifest: version.json_asset_on_release
versioning: auto_semver_patch_on_push
client: force_update_via_ApplyUpdate_bat
mvp: true
```

Канал (публичный репо):

1. `git push` в `main` (изменения в Assets/Packages/ProjectSettings/…)
2. Actions сам делает `patch` bump (`v0.1.0` → `v0.1.1`), собирает Windows, создаёт Release
3. Клиент: `GET /repos/Lizardan/BARAKI/releases/latest` → `version.json` → force update
4. Ручной major/minor: Actions → **Deploy Windows** → bump = minor/major
5. Пропуск релиза: commit message содержит `[skip release]`

## Non-goals

- Discord Activity / Embedded App SDK
- Unity WebGL ship client
- Dedicated server per match (production)
- Отдельный launcher exe
- Cloudflare R2 (требует карту на аккаунте)

## Locked decisions

| Решение | Значение |
|---------|----------|
| Ship client | **Windows x64 Standalone** |
| Net model | **Host-as-server** + Lobby + Relay |
| Social | **UGS Friends + Cloud Save** |
| Builds | **GHA → GitHub Releases → in-game force update** |
| Host migration / reconnect | **Post-MVP** |
