#!/usr/bin/env bash
# Deploy Activity shell + WebGL to Cloudflare Pages.
# Usage: from repo root after copying WebGL into web/activity-shell/Build/
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/web/activity-shell"
npx --yes wrangler pages deploy . --project-name "${PAGES_PROJECT:-baraki-activity}"
