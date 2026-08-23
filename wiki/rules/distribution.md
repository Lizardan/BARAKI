# Распространение клиента

Публичная страница: [https://baraki.pages.dev/](https://baraki.pages.dev/).
Кнопка «Скачать» бьёт в `/download` → 302 на GitHub asset
`updater-v*/BARAKI-Setup.exe` (prerelease апдейтера, **не** `/releases/latest`).

## Два канала

| Канал | Тег | Артефакт | Как появляется |
|-------|-----|----------|----------------|
| Полный клиент | `vX.Y.Z` | `BARAKI-vX.Y.Z.zip` | `deploy-windows.yml` на push в main |
| Апдейтер | `updater-vX.Y.Z` | `BARAKI-Setup.exe` | ручной `deploy-updater-windows.yml` |

`updater-v*` — всегда prerelease (`--latest=false`). Клиент обновлений
(`GameUpdateService`) смотрит только `/releases/latest` → zip полного клиента.

## Что ставит BARAKI-Setup.exe

Inno Setup (`Tooling/BuildSupport/Installer/BARAKI-Updater.iss`) пакует **тонкий**
Unity-клиент: сцена Bootstrap + define `BARAKI_UPDATER_ONLY`. Версия этого
билда `0.0.0`, поэтому лаунчер всегда видит апдейт и качает последний `v*` zip.
В меню «Войти в игру» апдейтер не пускает (`BootstrapUpdateFlowRules`).

Сборка: `Game.Editor.WindowsCiBuild.BuildUpdaterOnly` → `build/Updater` →
`ISCC.exe` → `dist/BARAKI-Setup.exe`. `AppId` стабильный, ставит в `{autopf}\BARAKI`.

## Деплой лендинга

Каталог: `Tooling/cloudflare/baraki-landing/`. Job `cloudflare` в updater-workflow
переписывает `functions/download.js` на свежий `updater-v*` и делает
`wrangler pages deploy`. Нужны секреты GitHub:

- `CLOUDFLARE_API_TOKEN` (Cloudflare Pages:Edit)
- `CLOUDFLARE_ACCOUNT_ID`
