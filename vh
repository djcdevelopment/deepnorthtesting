#!/usr/bin/env bash
# ==============================================================================
# ./vh — Turnkey Valheim 1.0.21 AI Modding Workbench CLI Wrapper
# ==============================================================================
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.local/share/discengine-dotnet}"
if [[ -d "$DOTNET_ROOT" ]]; then
  export PATH="$DOTNET_ROOT:$PATH"
fi
if [[ -d "$HOME/.dotnet/tools" ]]; then
  export PATH="$HOME/.dotnet/tools:$PATH"
fi
exec python3 "$SCRIPT_DIR/tools/vh.py" "$@"
