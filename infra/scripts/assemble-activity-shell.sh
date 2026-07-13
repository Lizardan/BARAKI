#!/usr/bin/env bash
# Copy Unity WebGL output into activity-shell/Build and optionally patch config.js
# Usage: ./infra/scripts/assemble-activity-shell.sh <webgl_dir> <shell_dir>
set -euo pipefail

WEBGL_DIR="${1:?webgl dir required}"
SHELL_DIR="${2:?shell dir required}"
BUILD_DIR="${SHELL_DIR}/Build"
mkdir -p "${BUILD_DIR}"

echo "Incoming WebGL tree (${WEBGL_DIR}):"
find "${WEBGL_DIR}" -maxdepth 4 \( -name '*.loader.js' -o -name 'index.html' -o -name '*.wasm*' -o -name '*.data*' \) 2>/dev/null | head -50 || true
ls -la "${WEBGL_DIR}" || true

SRC=""
LOADER=""

# Prefer known layouts, then any *.loader.js
if [[ -f "${WEBGL_DIR}/BARAKI.loader.js" ]]; then
  SRC="${WEBGL_DIR}"
  LOADER="${WEBGL_DIR}/BARAKI.loader.js"
elif [[ -f "${WEBGL_DIR}/Build/BARAKI.loader.js" ]]; then
  SRC="${WEBGL_DIR}/Build"
  LOADER="${WEBGL_DIR}/Build/BARAKI.loader.js"
elif [[ -f "${WEBGL_DIR}/BARAKI/Build/BARAKI.loader.js" ]]; then
  SRC="${WEBGL_DIR}/BARAKI/Build"
  LOADER="${WEBGL_DIR}/BARAKI/Build/BARAKI.loader.js"
else
  LOADER="$(find "${WEBGL_DIR}" -type f -name '*.loader.js' | head -1 || true)"
  if [[ -n "${LOADER}" ]]; then
    SRC="$(dirname "${LOADER}")"
  fi
fi

if [[ -z "${SRC}" || -z "${LOADER}" ]]; then
  echo "ERROR: no *.loader.js under ${WEBGL_DIR}"
  find "${WEBGL_DIR}" -type f | head -80 || true
  exit 1
fi

echo "Using loader: ${LOADER}"
echo "Copying WebGL from ${SRC} -> ${BUILD_DIR}"
# Clear previous build output but keep directory
find "${BUILD_DIR}" -mindepth 1 ! -name '.gitkeep' -delete
cp -a "${SRC}/." "${BUILD_DIR}/"

PRODUCT_BASE="$(basename "${LOADER}" .loader.js)"
if [[ "${PRODUCT_BASE}" != "BARAKI" ]]; then
  echo "Remapping product '${PRODUCT_BASE}' -> BARAKI in config.js"
  sed -i "s|BARAKI|${PRODUCT_BASE}|g" "${SHELL_DIR}/config.js"
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
