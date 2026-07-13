#!/usr/bin/env bash
# Copy Unity WebGL output into activity-shell/Build and optionally patch config.js
# Usage: ./infra/scripts/assemble-activity-shell.sh <webgl_dir> <shell_dir>
set -euo pipefail

WEBGL_DIR="${1:?webgl dir required}"
SHELL_DIR="${2:?shell dir required}"
BUILD_DIR="${SHELL_DIR}/Build"
mkdir -p "${BUILD_DIR}"

SRC=""
if [[ -f "${WEBGL_DIR}/BARAKI.loader.js" ]]; then
  SRC="${WEBGL_DIR}"
elif [[ -f "${WEBGL_DIR}/Build/BARAKI.loader.js" ]]; then
  SRC="${WEBGL_DIR}/Build"
elif [[ -f "${WEBGL_DIR}/BARAKI/Build/BARAKI.loader.js" ]]; then
  SRC="${WEBGL_DIR}/BARAKI/Build"
else
  echo "Looking for *.loader.js under ${WEBGL_DIR}:"
  find "${WEBGL_DIR}" -name "*.loader.js" | head -20 || true
  echo "ERROR: BARAKI.loader.js not found"
  exit 1
fi

echo "Copying WebGL from ${SRC} → ${BUILD_DIR}"
find "${BUILD_DIR}" -mindepth 1 -delete
cp -a "${SRC}/." "${BUILD_DIR}/"

if [[ ! -f "${BUILD_DIR}/BARAKI.loader.js" ]]; then
  LOADER="$(find "${BUILD_DIR}" -maxdepth 1 -name '*.loader.js' | head -1 || true)"
  if [[ -n "${LOADER}" ]]; then
    BASE="$(basename "${LOADER}" .loader.js)"
    echo "Remapping product '${BASE}' → paths in config.js"
    sed -i "s|BARAKI|${BASE}|g" "${SHELL_DIR}/config.js"
  fi
fi

if [[ -n "${DISCORD_CLIENT_ID:-}" ]]; then
  echo "Injecting DISCORD_CLIENT_ID into config.js"
  DISCORD_CLIENT_ID="${DISCORD_CLIENT_ID}" SHELL_DIR="${SHELL_DIR}" python3 - <<'PY'
import os, re
path = os.path.join(os.environ["SHELL_DIR"], "config.js")
cid = os.environ["DISCORD_CLIENT_ID"]
text = open(path, encoding="utf-8").read()
text2, n = re.subn(
    r'DISCORD_CLIENT_ID:\s*"[^"]*"',
    f'DISCORD_CLIENT_ID: "{cid}"',
    text,
    count=1,
)
if n == 0:
    raise SystemExit("DISCORD_CLIENT_ID field not found in config.js")
open(path, "w", encoding="utf-8").write(text2)
print("config.js updated")
PY
fi

echo "Activity shell ready at ${SHELL_DIR}"
ls -la "${BUILD_DIR}" | head -40
