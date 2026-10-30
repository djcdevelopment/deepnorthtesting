#!/usr/bin/env bash
# ==============================================================================
# switch-profile.sh — DeepNorthTesting Linux Zero-Copy Valheim Profile Engine
# ==============================================================================
# Sub-millisecond POSIX symlink profile switching (ln -sfn), ephemeral isolated
# mod profile creation, and live .cfg / .dll hotswapping for Valheim 1.0.21.
#
# Usage:
#   bash tools/switch-profile.sh --status
#   bash tools/switch-profile.sh --list
#   bash tools/switch-profile.sh --switch <profile_name> [--force]
#   bash tools/switch-profile.sh --create-isolated <name> <dll_path...>
#   bash tools/switch-profile.sh --hotswap <dll_or_cfg_path>
# ==============================================================================
set -euo pipefail

VALHEIM_DIR="${VALHEIM_DIR:-$HOME/.local/share/Steam/steamapps/common/Valheim}"
BEPINEX_DIR="$VALHEIM_DIR/BepInEx"
PLUGINS_LINK="$BEPINEX_DIR/plugins"
PROFILES_DIR="$BEPINEX_DIR/profiles"
CONFIG_DIR="$BEPINEX_DIR/config"

mkdir -p "$PROFILES_DIR" "$BEPINEX_DIR/profiles/clean-vanilla"

resolve_profile_target() {
  local name="$1"
  if [[ "$name" == "local" || "$name" == "plugins_local" || "$name" == "default" ]]; then
    echo "$BEPINEX_DIR/plugins_local"
    return 0
  fi
  if [[ -d "$PROFILES_DIR/$name" ]]; then
    echo "$PROFILES_DIR/$name"
    return 0
  fi
  if [[ -d "$PROFILES_DIR/isolated-$name" ]]; then
    echo "$PROFILES_DIR/isolated-$name"
    return 0
  fi
  return 1
}

show_status() {
  echo "======================================================================"
  echo "  DeepNorthTesting :: Linux Zero-Copy Profile Engine (Valheim 1.0.21)"
  echo "======================================================================"
  echo "  Valheim Root   : $VALHEIM_DIR"
  echo "  Plugins Path   : $PLUGINS_LINK"
  if [[ -L "$PLUGINS_LINK" ]]; then
    local target
    target="$(readlink "$PLUGINS_LINK")"
    echo "  Zero-Copy Link : YES (POSIX Symlink -> $target)"
    echo "  Active DLLs    :"
    find -L "$PLUGINS_LINK" -maxdepth 2 -name "*.dll" -printf "    - %f (%s bytes)\n" 2>/dev/null | sort || true
  elif [[ -d "$PLUGINS_LINK" ]]; then
    echo "  Zero-Copy Link : NO (Physical directory)"
  else
    echo "  Zero-Copy Link : MISSING"
  fi
  echo "======================================================================"
}

list_profiles() {
  echo "Available Valheim BepInEx Profiles:"
  local current_target=""
  if [[ -L "$PLUGINS_LINK" ]]; then
    current_target="$(readlink -f "$PLUGINS_LINK" || true)"
  fi
  for candidate in "$BEPINEX_DIR/plugins_local" "$PROFILES_DIR"/*; do
    [[ -d "$candidate" || -L "$candidate" ]] || continue
    local bname
    bname="$(basename "$candidate")"
    local resolved
    resolved="$(readlink -f "$candidate" || echo "$candidate")"
    local active_mark=""
    if [[ "$resolved" == "$current_target" ]]; then
      active_mark=" [ACTIVE]"
    fi
    local dll_count
    dll_count="$(find -L "$candidate" -maxdepth 2 -name "*.dll" 2>/dev/null | wc -l)"
    echo "  * ${bname}${active_mark} (${dll_count} DLLs -> ${candidate})"
  done
}

switch_profile() {
  local name="$1"
  local force="${2:-0}"
  if [[ "$force" != "1" ]] && pgrep -f "valheim.x86_64" >/dev/null 2>&1; then
    echo "[ERROR] Valheim (valheim.x86_64) is currently running. Close it or pass --force." >&2
    exit 1
  fi
  local target
  if ! target="$(resolve_profile_target "$name")"; then
    echo "[ERROR] Profile '$name' not found in $PROFILES_DIR" >&2
    exit 1
  fi
  local t0 t1 elapsed_us
  t0="$(date +%s%N)"
  ln -sfn "$target" "$PLUGINS_LINK"
  t1="$(date +%s%N)"
  elapsed_us=$(( (t1 - t0) / 1000 ))
  echo "[OK] Switched BepInEx/plugins -> $target in ${elapsed_us} us (0 bytes copied)"
}

create_isolated() {
  local name="$1"
  shift
  local iso_dir="$PROFILES_DIR/isolated-$name"
  rm -rf "$iso_dir"
  mkdir -p "$iso_dir"
  for dll in "$@"; do
    if [[ ! -f "$dll" ]]; then
      echo "[ERROR] DLL not found: $dll" >&2
      exit 1
    fi
    ln -sf "$(readlink -f "$dll")" "$iso_dir/$(basename "$dll")"
  done
  echo "[OK] Created zero-copy isolated profile: $iso_dir ($# DLLs linked)"
}

hotswap_file() {
  local src="$1"
  if [[ ! -f "$src" ]]; then
    echo "[ERROR] Hotswap file not found: $src" >&2
    exit 1
  fi
  local t0 t1 elapsed_us
  t0="$(date +%s%N)"
  case "$src" in
    *.cfg)
      mkdir -p "$CONFIG_DIR"
      cp -f "$src" "$CONFIG_DIR/$(basename "$src")"
      t1="$(date +%s%N)"
      elapsed_us=$(( (t1 - t0) / 1000 ))
      echo "[OK] Live-reloaded config $(basename "$src") -> $CONFIG_DIR in ${elapsed_us} us (FileSystemWatcher notified)"
      ;;
    *.dll)
      local active_dir
      active_dir="$(readlink -f "$PLUGINS_LINK")"
      ln -sf "$(readlink -f "$src")" "$active_dir/$(basename "$src")"
      t1="$(date +%s%N)"
      elapsed_us=$(( (t1 - t0) / 1000 ))
      echo "[OK] Staged zero-copy plugin symlink $(basename "$src") -> $active_dir in ${elapsed_us} us"
      ;;
    *)
      echo "[ERROR] Unsupported hotswap extension (expected .cfg or .dll): $src" >&2
      exit 1
      ;;
  esac
}

case "${1:---status}" in
  --status|-s)
    show_status
    ;;
  --list|-l)
    list_profiles
    ;;
  --switch)
    shift
    prof="${1:?Profile name required}"
    force_flag=0
    [[ "${2:-}" == "--force" ]] && force_flag=1
    switch_profile "$prof" "$force_flag"
    ;;
  --create-isolated)
    shift
    iso_name="${1:?Isolated profile name required}"
    shift
    create_isolated "$iso_name" "$@"
    ;;
  --hotswap)
    shift
    hotswap_file "${1:?Path to .cfg or .dll required}"
    ;;
  *)
    switch_profile "$1" "${2:-0}"
    ;;
esac
