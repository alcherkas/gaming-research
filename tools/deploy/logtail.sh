#!/usr/bin/env bash
# Stream the Unity Player.log from the Steam Deck over SSH.
# See tools/deploy/README.md for one-time SSH setup and usage.
#
# Usage (override any default via environment variable):
#   bash tools/deploy/logtail.sh
#   COMPANY=MyCompany PRODUCT=PhysicsProto DECK_HOST=192.168.1.50 bash tools/deploy/logtail.sh
set -euo pipefail

DECK_USER="${DECK_USER:-deck}"
DECK_HOST="${DECK_HOST:-steamdeck.local}"
# COMPANY/PRODUCT must match Unity Player Settings (used in the log path).
COMPANY="${COMPANY:-GamingResearch}"
PRODUCT="${PRODUCT:-PhysicsProto}"
LOG_PATH="${LOG_PATH:-/home/${DECK_USER}/.config/unity3d/${COMPANY}/${PRODUCT}/Player.log}"

echo "Tailing ${DECK_USER}@${DECK_HOST}:${LOG_PATH}"
exec ssh "${DECK_USER}@${DECK_HOST}" "tail -F '${LOG_PATH}'"
