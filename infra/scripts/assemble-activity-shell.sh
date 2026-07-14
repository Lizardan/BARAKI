#!/usr/bin/env bash
# Copy Unity WebGL output into activity-shell/Build and optionally patch config.js
# Usage: ./infra/scripts/assemble-activity-shell.sh <webgl_dir> <shell_dir>
set -euo pipefail

WEBGL_DIR="${1:?webgl dir required}"
SHELL_DIR="${2:?shell dir required}"
BUILD_DIR="${SHELL_DIR}/Build"
mkdir -p "${BUILD_DIR}"

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

# CI/Unity may name artifacts after the output folder (e.g. WebGL.*).
# Keep config.js / boot.js on stable BARAKI.* names — rename files, never rewrite BARAKI_CONFIG.
PRODUCT_BASE="$(basename "${LOADER}" .loader.js)"
if [[ "${PRODUCT_BASE}" != "BARAKI" ]]; then
  echo "Renaming '${PRODUCT_BASE}.*' -> 'BARAKI.*'"
  shopt -s nullglob
  for f in "${BUILD_DIR}/${PRODUCT_BASE}."*; do
    suffix="${f##*/${PRODUCT_BASE}.}"
    mv "${f}" "${BUILD_DIR}/BARAKI.${suffix}"
  done
  shopt -u nullglob
fi

if [[ ! -f "${BUILD_DIR}/BARAKI.loader.js" ]]; then
  echo "ERROR: expected ${BUILD_DIR}/BARAKI.loader.js after assemble"
  ls -la "${BUILD_DIR}" | head -40 || true
  exit 1
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

if [[ -n "${WSS_PROXY_TARGET:-}" ]]; then
  echo "Injecting WSS_PROXY_TARGET into config.js"
  WSS_PROXY_TARGET="${WSS_PROXY_TARGET}" SHELL_DIR="${SHELL_DIR}" python3 - <<'PY'
import os, re
path = os.path.join(os.environ["SHELL_DIR"], "config.js")
host = os.environ["WSS_PROXY_TARGET"]
text = open(path, encoding="utf-8").read()
text2, n = re.subn(
    r'WSS_PROXY_TARGET:\s*"[^"]*"',
    f'WSS_PROXY_TARGET: "{host}"',
    text,
    count=1,
)
if n == 0:
    raise SystemExit("WSS_PROXY_TARGET field not found in config.js")
open(path, "w", encoding="utf-8").write(text2)
print("WSS_PROXY_TARGET updated")
PY
fi

echo "Activity shell ready at ${SHELL_DIR}"
ls -la "${BUILD_DIR}" | head -40
