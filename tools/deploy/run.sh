#!/usr/bin/env bash
# Launch the deployed build on the Steam Deck over SSH, optionally with MangoHud
# (FPS / frametime / CPU / GPU / power(W) / temps) for efficiency debugging.
# Best run from the Deck in Desktop Mode context; for Gaming Mode, set MangoHud in the
# non-Steam game's launch options instead:  MANGOHUD=1 %command%
#
# Prereq on the Deck (once): install MangoHud from the Discover store.
#
# Usage:
#   bash tools/deploy/run.sh            # plain launch (streams stdout)
#   MANGO=1 bash tools/deploy/run.sh    # with MangoHud overlay
set -euo pipefail

DECK_USER="${DECK_USER:-deck}"
DECK_HOST="${DECK_HOST:-deck}"
GAME_NAME="${GAME_NAME:-PhysicsProto}"
REMOTE_DIR="${REMOTE_DIR:-/home/${DECK_USER}/Games/${GAME_NAME}}"
BIN="${GAME_NAME}.x86_64"

PREFIX=""
if [[ "${MANGO:-0}" == "1" ]]; then
  PREFIX="MANGOHUD=1"
  echo "Launching with MangoHud…"
fi

echo "Running ${REMOTE_DIR}/${BIN} on ${DECK_USER}@${DECK_HOST}"
exec ssh "${DECK_USER}@${DECK_HOST}" "cd '${REMOTE_DIR}' && ${PREFIX} ./'${BIN}'"
