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

Каталог: `Tooling/cloudflare/baraki-landing/`.
Страница — дневник (лента карточек) + правая колонка: скачать, расы, состав Людей.
Портреты: `public/art/races/humans/{Units|BonusUnits|Heroes}/`.
Кнопка «Скачать» ведёт на `/download` (Pages Function).

Прод: [https://baraki.pages.dev/](https://baraki.pages.dev/). Ветка `main` даёт
превью [https://main.baraki.pages.dev/](https://main.baraki.pages.dev/) — это не прод.
У проекта Pages production branch должен быть `main`; workflow выставляет его PATCH-ом.

Лендинг выкладывается отдельно: workflow `deploy-landing.yml` на push в
`Tooling/cloudflare/baraki-landing/` или вручную (`workflow_dispatch`).
Job `cloudflare` в updater-workflow дополнительно переписывает
`functions/download.js` на свежий `updater-v*` при релизе апдейтера.

Нужны секреты GitHub:

- `CLOUDFLARE_API_TOKEN` (шаблон Edit Cloudflare Workers)
- `CLOUDFLARE_ACCOUNT_ID`

## Деплой меню-чата

Каталог: `Tooling/cloudflare/baraki-chat/`.
Worker: [https://baraki-chat.lizard268.workers.dev](https://baraki-chat.lizard268.workers.dev).
Workflow: `deploy-chat.yml` (push в каталог / `workflow_dispatch`).
Доп. секреты: `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`. Аутентификация чата — UGS JWT (см. [`chat.md`](chat.md)).

Подробности API и клиента — [`chat.md`](chat.md).
