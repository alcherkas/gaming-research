#!/usr/bin/env bash
# Deploy the Linux x86_64 Unity build to a Steam Deck over SSH (rsync).
# See tools/deploy/README.md for one-time SSH setup and usage.
#
# Usage (override any default via environment variable):
#   bash tools/deploy/deploy.sh
#   DECK_HOST=192.168.1.50 GAME_NAME=PhysicsProto bash tools/deploy/deploy.sh
set -euo pipefail

DECK_USER="${DECK_USER:-deck}"
DECK_HOST="${DECK_HOST:-steamdeck.local}"
GAME_NAME="${GAME_NAME:-PhysicsProto}"
# Local folder containing <GAME_NAME>.x86_64, UnityPlayer.so, <GAME_NAME>_Data/, Plugins/
BUILD_DIR="${BUILD_DIR:-unity/Build/Linux}"
REMOTE_DIR="${REMOTE_DIR:-/home/${DECK_USER}/Games/${GAME_NAME}}"

if [[ ! -d "${BUILD_DIR}" ]]; then
  echo "error: build dir '${BUILD_DIR}' not found." >&2
  echo "       Build from Unity first (Linux / x86_64 / IL2CPP / Vulkan-first)." >&2
  exit 1
fi

echo "Deploying ${BUILD_DIR}/ -> ${DECK_USER}@${DECK_HOST}:${REMOTE_DIR}/"
ssh "${DECK_USER}@${DECK_HOST}" "mkdir -p '${REMOTE_DIR}'"
# -a preserves the exec bit Unity sets, so no chmod +x is needed after the first sync.
# Exclude Unity's non-shippable debug folders to keep the transfer lean.
# Note: macOS ships openrsync (no --info=progress2); -v lists transferred files.
rsync -avh --delete \
  --exclude='*_BurstDebugInformation_DoNotShip' \
  --exclude='*_BackUpThisFolder_ButDontShipItWithYourGame' \
  "${BUILD_DIR}/" "${DECK_USER}@${DECK_HOST}:${REMOTE_DIR}/"

echo
echo "Done. First time only, on the Deck (Desktop Mode):"
echo "  Steam > Add a Non-Steam Game > (set filter to All Files) > ${GAME_NAME}.x86_64"
