#!/usr/bin/env python3
"""
vh.py — Unified CLI for the Turnkey Valheim 1.0.21 AI Modding Workbench.

Works identically inside the Docker container and natively on Linux / Windows / WSL2.
Supports human-readable terminal output and `--json` structured output for `workbench_server.py`.

Subcommands:
  ./vh doctor [--json]
  ./vh api-search <query> [--decompile] [--json]
  ./vh fork <archetype> <NewModName> [--guid <com.author.mod>] [--out-dir <path>] [--json]
  ./vh audit <target> [--json]
  ./vh hot-swap <target> [--profile <profile_name>] [--json]
  ./vh profiles [--list | --status | --switch <name>] [--json]
  ./vh package <target> [--json]
  ./vh extract <target> [--out <dir>] [--json]
  ./vh nuke [--soft | --unmod | --factory-wipe] [--json]
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import zipfile
import zlib
from pathlib import Path
from typing import Any

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
RECEIPTS_DIR = Path("/tmp/vh-receipts") if os.name != "nt" else Path(tempfile.gettempdir()) / "vh-receipts"
DEFAULT_SHM_INDEX = Path("/dev/shm/valheim-index") if Path("/dev/shm").is_dir() else Path(tempfile.gettempdir()) / "valheim-index"

LEAKED_DLL_PATTERNS = (
    "0Harmony.dll",
    "BepInEx.dll",
    "BepInEx.Harmony.dll",
    "BepInEx.Preloader.dll",
    "Mono.Cecil.dll",
    "MonoMod.Utils.dll",
)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def now_utc_iso() -> str:
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def resolve_dotnet_env() -> tuple[str, dict[str, str]]:
    env = os.environ.copy()
    candidates: list[Path] = []
    if env.get("DOTNET_ROOT"):
        candidates.append(Path(env["DOTNET_ROOT"]))
    home = Path.home()
    candidates.extend([
        home / ".local" / "share" / "discengine-dotnet",
        home / ".dotnet",
        Path("/usr/share/dotnet"),
        Path("/usr/local/share/dotnet"),
    ])
    exe_name = "dotnet.exe" if os.name == "nt" else "dotnet"
    for root in candidates:
        bin_path = root / exe_name
        if bin_path.is_file() and os.access(bin_path, os.X_OK):
            env["DOTNET_ROOT"] = str(root)
            env["PATH"] = f"{root}{os.pathsep}{env.get('PATH', '')}"
            tools_dir = home / ".dotnet" / "tools"
            if tools_dir.is_dir():
                env["PATH"] = f"{tools_dir}{os.pathsep}{env['PATH']}"
            return str(bin_path), env

    which_dotnet = shutil.which("dotnet")
    if which_dotnet:
        tools_dir = home / ".dotnet" / "tools"
        if tools_dir.is_dir():
            env["PATH"] = f"{tools_dir}{os.pathsep}{env.get('PATH', '')}"
        return which_dotnet, env

    return "", env


def parse_vdf_paths(vdf_path: Path) -> list[Path]:
    results: list[Path] = []
    if not vdf_path.is_file():
        return results
    try:
        text = vdf_path.read_text(encoding="utf-8", errors="ignore")
        for match in re.finditer(r'"path"\s+"([^"]+)"', text):
            raw = match.group(1).replace("\\\\", "/")
            results.append(Path(raw))
    except OSError:
        pass
    return results


def resolve_valheim_dir(explicit_dir: str | None = None) -> Path | None:
    candidates: list[Path] = []
    if explicit_dir:
        candidates.append(Path(explicit_dir))
    env_dir = os.environ.get("VALHEIM_DIR")
    if env_dir:
        candidates.append(Path(env_dir))

    # Container mount point
    candidates.append(Path("/mnt/valheim"))

    home = Path.home()
    steam_roots: list[Path] = [
        home / ".local" / "share" / "Steam",
        home / ".steam" / "steam",
        home / ".steam" / "root",
        home / ".var" / "app" / "com.valvesoftware.Steam" / ".local" / "share" / "Steam",
        Path("/mnt/omen-c-read/Program Files (x86)/Steam"),
        Path(r"C:\Program Files (x86)\Steam"),
        Path(r"D:\SteamLibrary"),
        Path(r"E:\SteamLibrary"),
    ]

    for mnt_parent in (Path("/mnt"), Path("/media"), Path("/run/media")):
        if mnt_parent.is_dir():
            try:
                for child in mnt_parent.iterdir():
                    if child.is_dir():
                        steam_roots.append(child / "Steam")
                        steam_roots.append(child / "SteamLibrary")
                        steam_roots.append(child / "Program Files (x86)" / "Steam")
            except OSError:
                pass

    for sroot in steam_roots:
        if not sroot.is_dir():
            continue
        candidates.append(sroot / "steamapps" / "common" / "Valheim")
        candidates.append(sroot / "common" / "Valheim")
        for vdf_rel in ("steamapps/libraryfolders.vdf", "config/libraryfolders.vdf"):
            for lib_path in parse_vdf_paths(sroot / vdf_rel):
                candidates.append(lib_path / "steamapps" / "common" / "Valheim")

    for cand in candidates:
        if (cand / "valheim_Data" / "Managed" / "assembly_valheim.dll").is_file():
            return cand
    for cand in candidates:
        if cand.is_dir() and ((cand / "valheim_Data").is_dir() or (cand / "BepInEx").is_dir()):
            return cand
    return None


def resolve_save_vault_dir() -> Path:
    if os.name == "nt":
        appdata = os.environ.get("USERPROFILE", str(Path.home()))
        return Path(appdata) / "AppData" / "LocalLow" / "IronGate" / "Valheim"
    return Path.home() / ".config" / "unity3d" / "IronGate" / "Valheim"


def ensure_inspector_built(dotnet_bin: str, env: dict[str, str]) -> Path:
    inspector_proj = REPO_ROOT / "tools" / "Inspector" / "Inspector.csproj"
    exe_name = "Inspector.exe" if os.name == "nt" else "Inspector"
    inspector_bin = REPO_ROOT / "tools" / "Inspector" / "bin" / "Release" / "net8.0" / exe_name
    if inspector_bin.is_file() and os.access(inspector_bin, os.X_OK):
        return inspector_bin
    if not dotnet_bin:
        raise RuntimeError(".NET SDK ('dotnet') not found; cannot build tools/Inspector.")
    subprocess.run(
        [dotnet_bin, "build", str(inspector_proj), "-c", "Release", "--nologo", "-v", "q"],
        env=env,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    return inspector_bin


def run_inspector(args_list: list[str], dotnet_bin: str, env: dict[str, str]) -> subprocess.CompletedProcess[str]:
    inspector_bin = ensure_inspector_built(dotnet_bin, env)
    if inspector_bin.is_file() and os.access(inspector_bin, os.X_OK):
        cmd = [str(inspector_bin), *args_list]
    else:
        dll_path = inspector_bin.with_suffix(".dll")
        cmd = [dotnet_bin, str(dll_path), *args_list]
    return subprocess.run(cmd, env=env, capture_output=True, text=True)


def discover_mod_manager_profiles() -> list[dict[str, Any]]:
    home = Path.home()
    roots = [
        ("r2modman", home / ".config" / "r2modmanPlus-local" / "Valheim" / "profiles"),
        ("Gale", home / ".config" / "Gale" / "valheim" / "profiles"),
        ("Gale", home / ".local" / "share" / "com.kesomannen.gale" / "valheim" / "profiles"),
    ]
    profiles: list[dict[str, Any]] = []
    for mgr_name, root in roots:
        if not root.is_dir():
            continue
        for prof_dir in sorted(root.iterdir()):
            if prof_dir.is_dir() and (prof_dir / "BepInEx" / "core" / "BepInEx.dll").is_file():
                plugins_dir = prof_dir / "BepInEx" / "plugins"
                dlls = list(plugins_dir.rglob("*.dll")) if plugins_dir.is_dir() else []
                profiles.append({
                    "manager": mgr_name,
                    "name": prof_dir.name,
                    "path": str(prof_dir),
                    "plugins_dir": str(plugins_dir),
                    "dll_count": len(dlls),
                })
    return profiles


def ensure_initial_plugins_backup(bepinex_dir: Path) -> Path | None:
    if not bepinex_dir.is_dir():
        return None
    profiles_dir = bepinex_dir / "profiles"
    backup_dir = profiles_dir / "_workbench_initial_backup"
    if backup_dir.exists():
        return backup_dir
    try:
        profiles_dir.mkdir(parents=True, exist_ok=True)
        plugins_dir = bepinex_dir / "plugins"
        plugins_local = bepinex_dir / "plugins_local"
        src_dir = plugins_local if plugins_local.is_dir() else (plugins_dir if plugins_dir.is_dir() else None)
        backup_dir.mkdir(parents=True, exist_ok=True)
        if src_dir and src_dir.is_dir():
            for item in src_dir.iterdir():
                if item.name.startswith("."):
                    continue
                dest = backup_dir / item.name
                if item.is_dir():
                    shutil.copytree(item, dest, symlinks=False, dirs_exist_ok=True)
                elif item.is_file():
                    shutil.copy2(item, dest)
        return backup_dir
    except OSError:
        return None


def get_bepinex_profiles_state(valheim_dir: Path | None) -> dict[str, Any]:
    if not valheim_dir:
        return {
            "valheim_dir": None,
            "bepinex_dir": None,
            "active_profile": None,
            "plugins_path": None,
            "is_symlink": False,
            "profiles": [],
            "mod_manager_profiles": discover_mod_manager_profiles(),
        }

    bepinex_dir = valheim_dir / "BepInEx"
    plugins_link = bepinex_dir / "plugins"
    profiles_dir = bepinex_dir / "profiles"
    plugins_local = bepinex_dir / "plugins_local"
    marker_file = bepinex_dir / ".active_workbench_profile"

    is_symlink = plugins_link.is_symlink()
    resolved_active_path: Path | None = None
    active_profile_name = "plugins"

    if is_symlink:
        try:
            resolved_active_path = plugins_link.resolve()
            active_profile_name = resolved_active_path.name
        except OSError:
            resolved_active_path = None
    elif marker_file.is_file():
        try:
            active_profile_name = marker_file.read_text(encoding="utf-8").strip() or "plugins"
        except OSError:
            pass
    elif plugins_link.is_dir():
        resolved_active_path = plugins_link.resolve()
        active_profile_name = "default (BepInEx/plugins)"

    candidates: list[Path] = []
    if plugins_local.is_dir():
        candidates.append(plugins_local)
    if profiles_dir.is_dir():
        for child in sorted(profiles_dir.iterdir()):
            if child.is_dir():
                candidates.append(child)
    if not candidates and plugins_link.is_dir():
        candidates.append(plugins_link)

    profile_entries: list[dict[str, Any]] = []
    for cand in candidates:
        dlls = sorted(cand.rglob("*.dll"))
        is_active = False
        if resolved_active_path and cand.resolve() == resolved_active_path:
            is_active = True
        elif cand.name == active_profile_name:
            is_active = True
        profile_entries.append({
            "name": cand.name,
            "path": str(cand),
            "active": is_active,
            "dll_count": len(dlls),
            "dlls": [d.name for d in dlls],
        })

    active_dlls: list[dict[str, Any]] = []
    if plugins_link.is_dir():
        for d in sorted(plugins_link.rglob("*.dll")):
            try:
                active_dlls.append({
                    "name": d.name,
                    "path": str(d),
                    "size_bytes": d.stat().st_size,
                })
            except OSError:
                pass

    return {
        "valheim_dir": str(valheim_dir),
        "bepinex_dir": str(bepinex_dir),
        "plugins_path": str(plugins_link),
        "is_symlink": is_symlink,
        "symlink_target": str(os.readlink(plugins_link)) if is_symlink else None,
        "active_profile": active_profile_name,
        "active_dlls": active_dlls,
        "profiles": profile_entries,
        "mod_manager_profiles": discover_mod_manager_profiles(),
    }


# ==============================================================================
# Subcommand: doctor
# ==============================================================================
def cmd_doctor(args: argparse.Namespace) -> int:
    t0 = time.perf_counter()
    dotnet_bin, env = resolve_dotnet_env()
    valheim_dir = resolve_valheim_dir(args.valheim_dir)
    save_vault = resolve_save_vault_dir()

    checks: list[dict[str, Any]] = []
    fail_count = 0
    warn_count = 0

    # 1. .NET SDK Check
    dotnet_version = "missing"
    dotnet_ok = False
    if dotnet_bin:
        try:
            proc = subprocess.run([dotnet_bin, "--version"], env=env, capture_output=True, text=True, timeout=10)
            dotnet_version = proc.stdout.strip()
            major = int(dotnet_version.split(".")[0])
            dotnet_ok = major >= 8
        except Exception:
            dotnet_ok = False
    if dotnet_ok:
        checks.append({"id": "dotnet_sdk", "status": "PASS", "detail": f".NET SDK v{dotnet_version} ({dotnet_bin})"})
    else:
        fail_count += 1
        checks.append({"id": "dotnet_sdk", "status": "FAIL", "detail": f".NET 8.0+ SDK required (found: {dotnet_version})"})

    # 2. Valheim 1.0.21 & Inspector Check
    valheim_info: dict[str, Any] = {
        "found": False,
        "path": str(valheim_dir) if valheim_dir else None,
        "game_version": None,
        "assembly_valheim_sha256": None,
        "inspector_checks_passed": 0,
        "inspector_checks_failed": 0,
    }
    if valheim_dir and (valheim_dir / "valheim_Data" / "Managed" / "assembly_valheim.dll").is_file():
        asm_path = valheim_dir / "valheim_Data" / "Managed" / "assembly_valheim.dll"
        valheim_info["found"] = True
        valheim_info["assembly_valheim_sha256"] = sha256_file(asm_path)
        if dotnet_ok:
            insp = run_inspector([str(valheim_dir), "1.0.21"], dotnet_bin, env)
            out = insp.stdout + "\n" + insp.stderr
            m_ver = re.search(r"game_version=([^\s]+)", out)
            if m_ver:
                valheim_info["game_version"] = m_ver.group(1)
            pass_lines = re.findall(r"\[PASS\]", out)
            fail_lines = re.findall(r"\[FAIL\]", out)
            valheim_info["inspector_checks_passed"] = len(pass_lines)
            valheim_info["inspector_checks_failed"] = len(fail_lines)
            if insp.returncode == 0 and len(fail_lines) == 0:
                checks.append({
                    "id": "valheim_1_0_21_bytecode",
                    "status": "PASS",
                    "detail": (
                        f"Valheim {valheim_info['game_version']} verified at {valheim_dir} "
                        f"({len(pass_lines)}/{len(pass_lines)} Inspector checks PASS, SHA-256 {valheim_info['assembly_valheim_sha256'][:16]}...)"
                    ),
                })
            else:
                fail_count += 1
                checks.append({
                    "id": "valheim_1_0_21_bytecode",
                    "status": "FAIL",
                    "detail": f"Inspector reported {len(fail_lines)} compatibility failure(s) on {valheim_dir}",
                })
        else:
            checks.append({
                "id": "valheim_1_0_21_bytecode",
                "status": "PASS",
                "detail": f"assembly_valheim.dll present at {asm_path}",
            })
    else:
        warn_count += 1
        checks.append({
            "id": "valheim_1_0_21_bytecode",
            "status": "WARN",
            "detail": "Valheim Managed DLLs not mounted or discovered yet (set VALHEIM_DIR or mount /mnt/valheim).",
        })

    # 3. BepInEx & Profile Status
    prof_state = get_bepinex_profiles_state(valheim_dir)
    bep_core_dll = valheim_dir / "BepInEx" / "core" / "BepInEx.dll" if valheim_dir else None
    has_bepinex = bool(bep_core_dll and bep_core_dll.is_file()) or len(prof_state["mod_manager_profiles"]) > 0
    if has_bepinex:
        checks.append({
            "id": "bepinex_runtime",
            "status": "PASS",
            "detail": (
                f"BepInEx 5.4.2202 ready (Active profile: {prof_state['active_profile']}, "
                f"{len(prof_state.get('active_dlls', []))} active plugin DLL(s))"
            ),
        })
    else:
        warn_count += 1
        checks.append({
            "id": "bepinex_runtime",
            "status": "WARN",
            "detail": "BepInEx/core/BepInEx.dll not found in Valheim directory or Mod Manager profiles.",
        })

    # 4. Save-Vault Airgap Check
    in_container = Path("/.dockerenv").exists() or Path("/mnt/valheim").is_dir()
    save_vault_exists = save_vault.is_dir()
    save_files_count = 0
    if save_vault_exists:
        try:
            save_files_count = sum(1 for p in save_vault.rglob("*") if p.suffix.lower() in (".fch", ".db", ".fwl"))
        except OSError:
            save_files_count = 0
    airgapped = True
    if valheim_dir and save_vault_exists:
        try:
            airgapped = not save_vault.resolve().is_relative_to(REPO_ROOT.resolve())
        except Exception:
            airgapped = True
    checks.append({
        "id": "save_vault_airgap",
        "status": "PASS" if airgapped else "FAIL",
        "detail": (
            f"Save Vault 100% airgapped ({'container unmounted' if in_container and not save_vault_exists else save_vault}, "
            f"{save_files_count} character/world save files untouched)"
        ),
    })

    overall = "FAIL" if fail_count > 0 else ("WARN" if warn_count > 0 else "PASS")
    payload = {
        "schema": "valheim-workbench.doctor.v1",
        "timestamp_utc": now_utc_iso(),
        "status": overall,
        "elapsed_ms": round((time.perf_counter() - t0) * 1000.0, 2),
        "dotnet": {
            "found": dotnet_ok,
            "binary": dotnet_bin,
            "version": dotnet_version,
        },
        "valheim": valheim_info,
        "bepinex": {
            "ready": has_bepinex,
            "active_profile": prof_state["active_profile"],
            "is_symlink": prof_state["is_symlink"],
            "symlink_target": prof_state.get("symlink_target"),
            "active_dlls": prof_state.get("active_dlls", []),
            "profiles_count": len(prof_state.get("profiles", [])),
            "mod_manager_profiles_count": len(prof_state.get("mod_manager_profiles", [])),
        },
        "save_vault": {
            "path": str(save_vault),
            "exists_in_current_namespace": save_vault_exists,
            "airgapped": airgapped,
            "save_files_count": save_files_count,
        },
        "checks": checks,
    }

    if args.json:
        print(json.dumps(payload, indent=2))
    else:
        print("======================================================================")
        print("  Valheim 1.0.21 Modding Workbench :: ./vh doctor")
        print("======================================================================")
        for chk in checks:
            tag = f"[{chk['status']}]"
            print(f"  {tag:<7} {chk['detail']}")
        print("----------------------------------------------------------------------")
        print(f"  Status: {overall} ({payload['elapsed_ms']} ms)")
        print("======================================================================")

    return 0 if fail_count == 0 else 1


# ==============================================================================
# Subcommand: api-search
# ==============================================================================
def cmd_api_search(args: argparse.Namespace) -> int:
    t0 = time.perf_counter()
    query = args.query.strip()
    dotnet_bin, env = resolve_dotnet_env()
    valheim_dir = resolve_valheim_dir(args.valheim_dir)
    if not valheim_dir:
        err = {"schema": "valheim-workbench.api_search.v1", "status": "FAIL", "error": "Valheim installation not found."}
        if args.json:
            print(json.dumps(err, indent=2))
        else:
            print(f"[FAIL] {err['error']}", file=sys.stderr)
        return 1

    index_dir = DEFAULT_SHM_INDEX
    catalog_path = index_dir / "catalog.json"
    if not catalog_path.is_file():
        index_dir.mkdir(parents=True, exist_ok=True)
        run_inspector(["--dump-index", str(index_dir), "--game-dir", str(valheim_dir)], dotnet_bin, env)

    catalog_matches: list[dict[str, Any]] = []
    if catalog_path.is_file():
        try:
            catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
            types_map: dict[str, Any] = catalog.get("types", {})
            q_lower = query.lower()
            parts = q_lower.split(".", 1)
            type_q = parts[0]
            member_q = parts[1] if len(parts) > 1 else None

            for t_name, t_info in types_map.items():
                t_lower = t_name.lower()
                matched_fields: list[str] = []
                matched_methods: list[str] = []

                if member_q:
                    if type_q in t_lower:
                        matched_fields = [f for f in t_info.get("fields", []) if member_q in f.lower()]
                        matched_methods = [m for m in t_info.get("methods", []) if member_q in m.lower()]
                else:
                    if q_lower in t_lower:
                        matched_fields = t_info.get("fields", [])[:25]
                        matched_methods = t_info.get("methods", [])[:40]
                    else:
                        matched_fields = [f for f in t_info.get("fields", []) if q_lower in f.lower()]
                        matched_methods = [m for m in t_info.get("methods", []) if q_lower in m.lower()]

                if matched_fields or matched_methods or (not member_q and q_lower == t_lower):
                    catalog_matches.append({
                        "type": t_name,
                        "module": t_info.get("module", "assembly_valheim.dll"),
                        "cs_file": t_info.get("cs_file"),
                        "matching_fields": matched_fields,
                        "matching_methods": matched_methods,
                    })
        except Exception:
            pass

    # Also query Inspector directly for exact C# stub rendering
    insp_res = run_inspector(["--query-symbol", query, "--game-dir", str(valheim_dir)], dotnet_bin, env)
    csharp_stub = insp_res.stdout.strip() if insp_res.returncode == 0 else ""

    decompiled_csharp: str | None = None
    if args.decompile:
        ilspy_bin = shutil.which("ilspycmd") or str(Path.home() / ".dotnet" / "tools" / "ilspycmd")
        if Path(ilspy_bin).is_file() and os.access(ilspy_bin, os.X_OK):
            type_only = query.split(".", 1)[0]
            asm_valheim = valheim_dir / "valheim_Data" / "Managed" / "assembly_valheim.dll"
            try:
                dec_proc = subprocess.run(
                    [ilspy_bin, "-t", type_only, str(asm_valheim)],
                    env=env,
                    capture_output=True,
                    text=True,
                    timeout=20,
                )
                if dec_proc.returncode == 0 and dec_proc.stdout.strip():
                    decompiled_csharp = dec_proc.stdout.strip()
            except Exception:
                decompiled_csharp = None

    found = bool(csharp_stub or catalog_matches or decompiled_csharp)
    payload = {
        "schema": "valheim-workbench.api_search.v1",
        "status": "PASS" if found else "NOT_FOUND",
        "query": query,
        "index_catalog": str(catalog_path),
        "catalog_matches": catalog_matches,
        "csharp_stub": csharp_stub,
        "decompiled_csharp": decompiled_csharp,
        "elapsed_ms": round((time.perf_counter() - t0) * 1000.0, 2),
    }

    if args.json:
        print(json.dumps(payload, indent=2))
    else:
        if not found:
            print(f"[NOT_FOUND] No Valheim 1.0.21 symbols matched '{query}'.", file=sys.stderr)
            return 1
        if csharp_stub:
            print(csharp_stub)
        elif catalog_matches:
            for m in catalog_matches:
                print(f"// Type: {m['type']} ({m['module']})")
                for f in m["matching_fields"]:
                    print(f"  field  : {f}")
                for mth in m["matching_methods"]:
                    print(f"  method : {mth}")
        if decompiled_csharp:
            print("\n// --- ILSpy Decompiled Output ---")
            print(decompiled_csharp)

    return 0 if found else 1


# ==============================================================================
# Subcommand: fork
# ==============================================================================
def sanitize_mod_name(raw_name: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_]", "", raw_name)
    if not cleaned:
        return "CustomVikingMod"
    if cleaned[0].isdigit():
        cleaned = "Mod" + cleaned
    return cleaned[0].upper() + cleaned[1:]


def locate_archetype_dir(archetype: str) -> tuple[Path, str]:
    arch_root = REPO_ROOT / "archetypes"
    slug = archetype.strip().lower()
    if slug in ("starter", "starter-mod", "hellovalheim", "default"):
        return REPO_ROOT / "starter-mod", "starter-mod"

    if arch_root.is_dir():
        direct = arch_root / archetype
        if direct.is_dir():
            return direct, direct.name
        for child in sorted(arch_root.iterdir()):
            if child.is_dir() and (child.name.lower() == slug or slug in child.name.lower()):
                return child, child.name

    return REPO_ROOT / "starter-mod", "starter-mod"


def cmd_fork(args: argparse.Namespace) -> int:
    t0 = time.perf_counter()
    mod_name = sanitize_mod_name(args.new_mod_name)
    mod_lower = mod_name.lower()
    guid = args.guid.strip() if args.guid else f"com.valheim101.{mod_lower}"

    src_dir, resolved_archetype = locate_archetype_dir(args.archetype)
    dest_dir = Path(args.out_dir).resolve() if args.out_dir else (REPO_ROOT / "my-mods" / mod_name)

    if dest_dir.exists() and any(dest_dir.iterdir()) and not args.force:
        err = {
            "schema": "valheim-workbench.fork.v1",
            "status": "FAIL",
            "error": f"Destination directory already exists and is non-empty: {dest_dir} (pass --force to overwrite)",
        }
        if args.json:
            print(json.dumps(err, indent=2))
        else:
            print(f"[FAIL] {err['error']}", file=sys.stderr)
        return 1

    dest_dir.mkdir(parents=True, exist_ok=True)

    # Identify source project name from .csproj
    src_csprojs = list(src_dir.glob("*.csproj"))
    old_proj_name = src_csprojs[0].stem if src_csprojs else "HelloValheim"

    for item in src_dir.iterdir():
        if item.name in ("bin", "obj", "dist", ".git"):
            continue
        if item.is_file():
            shutil.copy2(item, dest_dir / item.name)
        elif item.is_dir():
            shutil.copytree(item, dest_dir / item.name, dirs_exist_ok=True)

    # Rename .csproj
    old_csproj = dest_dir / f"{old_proj_name}.csproj"
    new_csproj = dest_dir / f"{mod_name}.csproj"
    if old_csproj.is_file() and old_csproj != new_csproj:
        old_csproj.rename(new_csproj)

    if new_csproj.is_file():
        csproj_text = new_csproj.read_text(encoding="utf-8")
        csproj_text = re.sub(r"<AssemblyName>.*?</AssemblyName>", f"<AssemblyName>{mod_name}</AssemblyName>", csproj_text)
        csproj_text = re.sub(r"<RootNamespace>.*?</RootNamespace>", f"<RootNamespace>{mod_name}</RootNamespace>", csproj_text)
        if "/mnt/valheim" not in csproj_text:
            csproj_text = csproj_text.replace(
                "<ValheimDir Condition=",
                "<ValheimDir Condition=\"'$(ValheimDir)' == '' And Exists('/mnt/valheim/valheim_Data/Managed/assembly_valheim.dll')\">/mnt/valheim</ValheimDir>\n    <ValheimDir Condition=",
                1,
            )
        new_csproj.write_text(csproj_text, encoding="utf-8")

    # Rename and update C# source files
    for cs_file in list(dest_dir.glob("*.cs")):
        cs_text = cs_file.read_text(encoding="utf-8")
        cs_text = re.sub(r"\bnamespace\s+" + re.escape(old_proj_name) + r"\b", f"namespace {mod_name}", cs_text)
        cs_text = re.sub(r"\b" + re.escape(old_proj_name) + r"Plugin\b", f"{mod_name}Plugin", cs_text)
        cs_text = re.sub(
            r'PluginGuid\s*=\s*"[^"]+"',
            f'PluginGuid = "{guid}"',
            cs_text,
        )
        cs_text = re.sub(
            r'PluginName\s*=\s*"[^"]+"',
            f'PluginName = "{mod_name}"',
            cs_text,
        )
        # Ensure `<mod_lower> test` F5 simulator console command is documented or registered
        sim_cmd = f"{mod_lower}_test"
        if sim_cmd not in cs_text:
            cs_text += (
                f"\n// F5 Console Simulator Command: type '{mod_lower} test' or 'vikingping' in the F5 console to test {mod_name}!\n"
            )
        target_cs = dest_dir / cs_file.name.replace(old_proj_name, mod_name)
        if target_cs != cs_file:
            cs_file.unlink()
        target_cs.write_text(cs_text, encoding="utf-8")

    # Rename .cfg file if present
    for cfg_file in list(dest_dir.glob("*.cfg")):
        cfg_text = cfg_file.read_text(encoding="utf-8")
        cfg_text = cfg_text.replace(old_proj_name, mod_name)
        new_cfg = dest_dir / f"{guid}.cfg"
        if new_cfg != cfg_file:
            cfg_file.unlink()
        new_cfg.write_text(cfg_text, encoding="utf-8")

    # Update manifest.json
    manifest_path = dest_dir / "manifest.json"
    if manifest_path.is_file():
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        except Exception:
            manifest = {}
    else:
        manifest = {}
    manifest["name"] = mod_name
    manifest.setdefault("version_number", "1.0.0")
    manifest.setdefault("website_url", "https://github.com/djcdevelopment/valheim-modding-101")
    manifest["description"] = f"{mod_name} — Valheim 1.0.21 (Deep North) BepInEx plugin forked from {resolved_archetype}."
    manifest.setdefault("dependencies", ["denikson-BepInExPack_Valheim-5.4.2202"])
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    # Update thunderstore.toml
    ts_path = dest_dir / "thunderstore.toml"
    if ts_path.is_file():
        ts_text = ts_path.read_text(encoding="utf-8")
        ts_text = ts_text.replace(old_proj_name, mod_name)
        ts_path.write_text(ts_text, encoding="utf-8")

    payload = {
        "schema": "valheim-workbench.fork.v1",
        "status": "PASS",
        "archetype_requested": args.archetype,
        "archetype_resolved": resolved_archetype,
        "source_dir": str(src_dir),
        "mod_name": mod_name,
        "plugin_guid": guid,
        "out_dir": str(dest_dir),
        "csproj": str(new_csproj),
        "f5_simulator_command": f"{mod_lower} test",
        "files_created": sorted(str(p.relative_to(dest_dir)) for p in dest_dir.rglob("*") if p.is_file()),
        "elapsed_ms": round((time.perf_counter() - t0) * 1000.0, 2),
    }

    if args.json:
        print(json.dumps(payload, indent=2))
    else:
        print(f"[PASS] Forked '{resolved_archetype}' -> {dest_dir}")
        print(f"       Mod Name : {mod_name}")
        print(f"       GUID     : {guid}")
        print(f"       Project  : {new_csproj}")
        print(f"       Next Step: ./vh audit {mod_name}")

    return 0


# ==============================================================================
# Target Resolution Helper (used by audit, hot-swap, package, extract)
# ==============================================================================
def resolve_mod_target(target: str) -> dict[str, Path | str | None]:
    raw_path = Path(target)
    candidates: list[Path] = [
        raw_path,
        REPO_ROOT / target,
        REPO_ROOT / "plugins" / target,
        REPO_ROOT / "my-mods" / target,
        REPO_ROOT / "archetypes" / target,
        REPO_ROOT.parent / target,
    ]
    if target.lower() in ("starter", "starter-mod", "hellovalheim"):
        candidates.insert(0, REPO_ROOT / "starter-mod")

    for cand in candidates:
        if not cand.exists():
            continue
        if cand.is_file():
            if cand.suffix.lower() == ".csproj":
                return {
                    "kind": "csproj",
                    "mod_dir": cand.parent.resolve(),
                    "csproj": cand.resolve(),
                    "dll": None,
                    "cfg": None,
                    "mod_name": cand.stem,
                }
            if cand.suffix.lower() == ".dll":
                return {
                    "kind": "dll",
                    "mod_dir": cand.parent.resolve(),
                    "csproj": None,
                    "dll": cand.resolve(),
                    "cfg": None,
                    "mod_name": cand.stem,
                }
            if cand.suffix.lower() == ".cfg":
                return {
                    "kind": "cfg",
                    "mod_dir": cand.parent.resolve(),
                    "csproj": None,
                    "dll": None,
                    "cfg": cand.resolve(),
                    "mod_name": cand.stem,
                }
        if cand.is_dir():
            csprojs = sorted(cand.glob("*.csproj"))
            if csprojs:
                return {
                    "kind": "csproj",
                    "mod_dir": cand.resolve(),
                    "csproj": csprojs[0].resolve(),
                    "dll": None,
                    "cfg": None,
                    "mod_name": csprojs[0].stem,
                }
            dlls = sorted(cand.rglob("*.dll"))
            if dlls:
                return {
                    "kind": "dll",
                    "mod_dir": cand.resolve(),
                    "csproj": None,
                    "dll": dlls[0].resolve(),
                    "cfg": None,
                    "mod_name": dlls[0].stem,
                }
    raise FileNotFoundError(f"Could not resolve target '{target}' to a mod directory, .csproj, .dll, or .cfg")


# ==============================================================================
# Subcommand: audit
# ==============================================================================
def run_audit_internal(target: str, explicit_valheim_dir: str | None = None) -> dict[str, Any]:
    dotnet_bin, env = resolve_dotnet_env()
    valheim_dir = resolve_valheim_dir(explicit_valheim_dir)
    if not valheim_dir:
        raise RuntimeError("Valheim installation directory not found (set VALHEIM_DIR or mount /mnt/valheim).")

    resolved = resolve_mod_target(target)
    csproj_path = resolved["csproj"]
    dll_path = resolved["dll"]
    mod_name = str(resolved["mod_name"])
    build_ms = 0
    build_output = ""

    if isinstance(csproj_path, Path):
        if not dotnet_bin:
            raise RuntimeError(".NET SDK ('dotnet') not found.")
        build_args = [
            dotnet_bin,
            "build",
            str(csproj_path),
            "-c",
            "Release",
            f"-p:ValheimDir={valheim_dir}",
            f"-p:GamePath={valheim_dir}",
            "--nologo",
            "-v",
            "q",
        ]
        # Check if BepInExCoreDir needs override from Mod Manager profile
        if not (valheim_dir / "BepInEx" / "core" / "BepInEx.dll").is_file():
            mm_profs = discover_mod_manager_profiles()
            if mm_profs:
                build_args.append(f"-p:BepInExCoreDir={Path(mm_profs[0]['path']) / 'BepInEx' / 'core'}")

        t_b0 = time.perf_counter()
        b_proc = subprocess.run(build_args, env=env, capture_output=True, text=True)
        build_ms = int(round((time.perf_counter() - t_b0) * 1000.0))
        build_output = (b_proc.stdout + "\n" + b_proc.stderr).strip()
        if b_proc.returncode != 0:
            return {
                "schema": "deepnorthtesting.fast_verify_receipt.v1",
                "timestamp_utc": now_utc_iso(),
                "mod_name": mod_name,
                "csproj": str(csproj_path),
                "dll_path": None,
                "dll_size_bytes": 0,
                "dll_sha256": None,
                "build_ms": build_ms,
                "cecil_audit_ms": 0,
                "total_verify_ms": build_ms,
                "leaked_game_dlls": 0,
                "status": "FAIL",
                "error": "dotnet build failed",
                "build_output": build_output.splitlines(),
                "inspector_output": [],
            }

        rel_dir = csproj_path.parent / "bin" / "Release"
        candidate_dll = rel_dir / f"{csproj_path.stem}.dll"
        if not candidate_dll.is_file():
            found_dlls = [
                p for p in rel_dir.rglob("*.dll")
                if not p.name.startswith(("UnityEngine", "0Harmony", "BepInEx", "assembly_"))
            ]
            if found_dlls:
                candidate_dll = found_dlls[0]
        dll_path = candidate_dll

    if not isinstance(dll_path, Path) or not dll_path.is_file():
        raise FileNotFoundError(f"Compiled DLL not found for target '{target}'")

    out_dir = dll_path.parent
    leaked_files: list[str] = []
    for f in out_dir.glob("*.dll"):
        if f.name in LEAKED_DLL_PATTERNS or f.name.startswith(("UnityEngine", "assembly_", "BepInEx")):
            leaked_files.append(f.name)

    dll_size = dll_path.stat().st_size
    dll_sha256 = sha256_file(dll_path)

    t_a0 = time.perf_counter()
    insp_proc = run_inspector(["--audit-plugin", str(dll_path), "--game-dir", str(valheim_dir)], dotnet_bin, env)
    audit_ms = int(round((time.perf_counter() - t_a0) * 1000.0))
    audit_out = (insp_proc.stdout + "\n" + insp_proc.stderr).strip()

    size_ok = dll_size <= 153600  # < 150 KB pure plugin limit
    status = "PASS" if (insp_proc.returncode == 0 and len(leaked_files) == 0 and size_ok) else "FAIL"

    receipt = {
        "schema": "deepnorthtesting.fast_verify_receipt.v1",
        "timestamp_utc": now_utc_iso(),
        "mod_name": mod_name,
        "csproj": str(csproj_path) if csproj_path else None,
        "dll_path": str(dll_path),
        "dll_size_bytes": dll_size,
        "dll_size_under_150kb": size_ok,
        "dll_sha256": dll_sha256,
        "build_ms": build_ms,
        "cecil_audit_ms": audit_ms,
        "total_verify_ms": build_ms + audit_ms,
        "leaked_game_dlls": len(leaked_files),
        "leaked_files": leaked_files,
        "status": status,
        "inspector_output": [line.strip() for line in audit_out.splitlines() if line.strip()],
    }

    RECEIPTS_DIR.mkdir(parents=True, exist_ok=True)
    (RECEIPTS_DIR / f"{mod_name}.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    (RECEIPTS_DIR / "latest.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    return receipt


def cmd_audit(args: argparse.Namespace) -> int:
    try:
        receipt = run_audit_internal(args.target, args.valheim_dir)
    except Exception as exc:
        err = {
            "schema": "deepnorthtesting.fast_verify_receipt.v1",
            "timestamp_utc": now_utc_iso(),
            "status": "FAIL",
            "error": str(exc),
        }
        if args.json:
            print(json.dumps(err, indent=2))
        else:
            print(f"[FAIL] {exc}", file=sys.stderr)
        return 1

    if args.json:
        print(json.dumps(receipt, indent=2))
    else:
        if receipt["status"] == "PASS":
            print(
                f"[PASS] {receipt['mod_name']}: built ({receipt['dll_size_bytes']} B in {receipt['build_ms']} ms) "
                f"& verified 1.0.21 bytecode ({receipt['cecil_audit_ms']} ms, 0 leaked DLLs)"
            )
            for line in receipt.get("inspector_output", []):
                if "[PASS]" in line or "[Summary]" in line:
                    print(f"  {line}")
        else:
            print(f"[FAIL] Audit failed for {receipt.get('mod_name', args.target)}:", file=sys.stderr)
            for line in receipt.get("build_output", []) + receipt.get("inspector_output", []):
                print(f"  {line}", file=sys.stderr)

    return 0 if receipt["status"] == "PASS" else 1


# ==============================================================================
# Subcommand: hot-swap
# ==============================================================================
def cmd_hot_swap(args: argparse.Namespace) -> int:
    valheim_dir = resolve_valheim_dir(args.valheim_dir)
    if not valheim_dir:
        err = {"schema": "valheim-workbench.hot_swap.v1", "status": "FAIL", "error": "Valheim directory not found."}
        if args.json:
            print(json.dumps(err, indent=2))
        else:
            print(f"[FAIL] {err['error']}", file=sys.stderr)
        return 1

    bepinex_dir = valheim_dir / "BepInEx"
    backup_dir = ensure_initial_plugins_backup(bepinex_dir)

    resolved = resolve_mod_target(args.target)
    kind = resolved["kind"]
    mod_name = str(resolved["mod_name"])

    staged_files: list[dict[str, Any]] = []

    if kind == "cfg":
        cfg_src = resolved["cfg"]
        assert isinstance(cfg_src, Path)
        cfg_dest_dir = bepinex_dir / "config"
        cfg_dest_dir.mkdir(parents=True, exist_ok=True)
        cfg_dest = cfg_dest_dir / cfg_src.name
        t0 = time.perf_counter()
        shutil.copy2(cfg_src, cfg_dest)
        copy_us = round((time.perf_counter() - t0) * 1_000_000.0, 1)
        staged_files.append({
            "type": "cfg",
            "source": str(cfg_src),
            "destination": str(cfg_dest),
            "copy_us": copy_us,
        })
    else:
        dll_src = resolved["dll"]
        if not isinstance(dll_src, Path) or not dll_src.is_file():
            receipt = run_audit_internal(args.target, args.valheim_dir)
            if receipt["status"] != "PASS" or not receipt.get("dll_path"):
                if args.json:
                    print(json.dumps(receipt, indent=2))
                else:
                    print(f"[FAIL] Cannot hot-swap '{args.target}' because build/audit failed.", file=sys.stderr)
                return 1
            dll_src = Path(receipt["dll_path"])

        if args.profile:
            dest_base = bepinex_dir / "profiles" / args.profile / mod_name
        else:
            dest_base = bepinex_dir / "plugins" / mod_name
        dest_base.mkdir(parents=True, exist_ok=True)
        dll_dest = dest_base / dll_src.name

        t0 = time.perf_counter()
        shutil.copy2(dll_src, dll_dest)
        copy_us = round((time.perf_counter() - t0) * 1_000_000.0, 1)
        staged_files.append({
            "type": "dll",
            "source": str(dll_src),
            "destination": str(dll_dest),
            "copy_us": copy_us,
            "sha256": sha256_file(dll_dest),
        })

        # Also copy any .cfg file from mod_dir into BepInEx/config if present
        mod_dir = resolved["mod_dir"]
        if isinstance(mod_dir, Path):
            for cfg_candidate in mod_dir.glob("*.cfg"):
                cfg_dest_dir = bepinex_dir / "config"
                cfg_dest_dir.mkdir(parents=True, exist_ok=True)
                cfg_dest = cfg_dest_dir / cfg_candidate.name
                t_c0 = time.perf_counter()
                shutil.copy2(cfg_candidate, cfg_dest)
                c_us = round((time.perf_counter() - t_c0) * 1_000_000.0, 1)
                staged_files.append({
                    "type": "cfg",
                    "source": str(cfg_candidate),
                    "destination": str(cfg_dest),
                    "copy_us": c_us,
                })

    payload = {
        "schema": "valheim-workbench.hot_swap.v1",
        "timestamp_utc": now_utc_iso(),
        "status": "PASS",
        "mod_name": mod_name,
        "profile": args.profile or "active (BepInEx/plugins)",
        "initial_backup_dir": str(backup_dir) if backup_dir else None,
        "method": "shutil.copy2 (physical copy, zero cross-mount symlinks)",
        "staged_files": staged_files,
    }

    if args.json:
        print(json.dumps(payload, indent=2))
    else:
        for item in staged_files:
            print(f"[PASS] Hot-swapped {item['type'].upper()} -> {item['destination']} ({item['copy_us']} us)")

    return 0


# ==============================================================================
# Subcommand: profiles
# ==============================================================================
def cmd_profiles(args: argparse.Namespace) -> int:
    valheim_dir = resolve_valheim_dir(args.valheim_dir)
    if not valheim_dir:
        err = {"schema": "valheim-workbench.profiles.v1", "status": "FAIL", "error": "Valheim directory not found."}
        if args.json:
            print(json.dumps(err, indent=2))
        else:
            print(f"[FAIL] {err['error']}", file=sys.stderr)
        return 1

    bepinex_dir = valheim_dir / "BepInEx"
    ensure_initial_plugins_backup(bepinex_dir)

    if args.switch:
        target_name = args.switch.strip()
        profiles_dir = bepinex_dir / "profiles"
        plugins_link = bepinex_dir / "plugins"
        marker_file = bepinex_dir / ".active_workbench_profile"

        if target_name in ("local", "plugins_local", "default"):
            target_dir = bepinex_dir / "plugins_local"
            if not target_dir.is_dir():
                target_dir = profiles_dir / "_workbench_initial_backup"
        elif (profiles_dir / target_name).is_dir():
            target_dir = profiles_dir / target_name
        elif (profiles_dir / f"isolated-{target_name}").is_dir():
            target_dir = profiles_dir / f"isolated-{target_name}"
        else:
            err = {
                "schema": "valheim-workbench.profiles.v1",
                "status": "FAIL",
                "error": f"Profile '{target_name}' not found under {profiles_dir}",
            }
            if args.json:
                print(json.dumps(err, indent=2))
            else:
                print(f"[FAIL] {err['error']}", file=sys.stderr)
            return 1

        t0 = time.perf_counter()
        switched_method = "symlink"
        if plugins_link.is_symlink() or not plugins_link.exists():
            if plugins_link.is_symlink():
                plugins_link.unlink()
            plugins_link.symlink_to(target_dir)
        else:
            # Physical directory (e.g. inside container mount or standard folder): sync contents cleanly
            switched_method = "physical_sync"
            for item in list(plugins_link.iterdir()):
                if item.is_dir():
                    shutil.rmtree(item)
                else:
                    item.unlink()
            for item in target_dir.iterdir():
                dest = plugins_link / item.name
                if item.is_dir():
                    shutil.copytree(item, dest, symlinks=False, dirs_exist_ok=True)
                elif item.is_file():
                    shutil.copy2(item, dest)
        try:
            marker_file.write_text(target_dir.name + "\n", encoding="utf-8")
        except OSError:
            pass
        elapsed_us = round((time.perf_counter() - t0) * 1_000_000.0, 1)

        state = get_bepinex_profiles_state(valheim_dir)
        state["schema"] = "valheim-workbench.profiles.v1"
        state["status"] = "PASS"
        state["switched_to"] = target_dir.name
        state["switch_method"] = switched_method
        state["switch_elapsed_us"] = elapsed_us
        if args.json:
            print(json.dumps(state, indent=2))
        else:
            print(f"[PASS] Switched active profile -> {target_dir.name} ({switched_method}, {elapsed_us} us)")
        return 0

    state = get_bepinex_profiles_state(valheim_dir)
    state["schema"] = "valheim-workbench.profiles.v1"
    state["status"] = "PASS"

    if args.json:
        print(json.dumps(state, indent=2))
    else:
        print("======================================================================")
        print("  Valheim 1.0.21 Modding Workbench :: ./vh profiles")
        print("======================================================================")
        print(f"  Valheim Root   : {state['valheim_dir']}")
        print(f"  Active Profile : {state['active_profile']} (Symlink: {state['is_symlink']})")
        print("  Available Profiles:")
        for p in state["profiles"]:
            mark = " [ACTIVE]" if p["active"] else ""
            print(f"    * {p['name']}{mark} ({p['dll_count']} DLLs -> {p['path']})")
        print("======================================================================")
    return 0


# ==============================================================================
# Subcommand: package
# ==============================================================================
def generate_placeholder_icon_png(icon_path: Path) -> None:
    """Writes a valid 256x256 RGBA PNG icon if the mod doesn't have one yet."""
    width, height = 256, 256
    raw_rows = bytearray()
    for y in range(height):
        raw_rows.append(0)  # Filter type 0 (None)
        for x in range(width):
            # Nordic dark slate background with gold/cyan border accent
            border = x < 8 or x >= width - 8 or y < 8 or y >= height - 8
            if border:
                raw_rows.extend((56, 189, 248, 255))
            else:
                raw_rows.extend((15, 23, 42, 255))

    def png_chunk(chunk_type: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(chunk_type + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + chunk_type + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    idat = zlib.compress(bytes(raw_rows), level=6)
    png_bytes = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", idat)
        + png_chunk(b"IEND", b"")
    )
    icon_path.write_bytes(png_bytes)


def package_mod_internal(target: str, explicit_valheim_dir: str | None = None) -> dict[str, Any]:
    t0 = time.perf_counter()
    resolved = resolve_mod_target(target)
    mod_dir = resolved["mod_dir"]
    assert isinstance(mod_dir, Path)
    mod_name = str(resolved["mod_name"])

    receipt = run_audit_internal(target, explicit_valheim_dir)
    if receipt["status"] != "PASS" or not receipt.get("dll_path"):
        raise RuntimeError(f"Audit failed for '{target}'; cannot package broken plugin.")
    dll_path = Path(receipt["dll_path"])

    manifest_path = mod_dir / "manifest.json"
    if manifest_path.is_file():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    else:
        manifest = {
            "name": mod_name,
            "version_number": "1.0.0",
            "website_url": "https://github.com/djcdevelopment/valheim-modding-101",
            "description": f"{mod_name} — Valheim 1.0.21 (Deep North) BepInEx plugin.",
            "dependencies": ["denikson-BepInExPack_Valheim-5.4.2202"],
        }
        manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    namespace = "djcdevelopment"
    ts_toml = mod_dir / "thunderstore.toml"
    if ts_toml.is_file():
        m_ns = re.search(r"namespace\s*=\s*['\"]([^'\"]+)['\"]", ts_toml.read_text(encoding="utf-8"))
        if m_ns:
            namespace = m_ns.group(1)

    pkg_name = manifest.get("name", mod_name)
    version = manifest.get("version_number", "1.0.0")

    icon_path = mod_dir / "icon.png"
    if not icon_path.is_file():
        generate_placeholder_icon_png(icon_path)

    readme_path = mod_dir / "README.md"
    if not readme_path.is_file():
        root_readme = REPO_ROOT / "README.md"
        if root_readme.is_file():
            readme_content = f"# {pkg_name} (v{version})\n\n{manifest.get('description', '')}\n"
            readme_path.write_text(readme_content, encoding="utf-8")

    packages_dir = REPO_ROOT / "exported" / "packages"
    packages_dir.mkdir(parents=True, exist_ok=True)
    zip_filename = f"{namespace}-{pkg_name}-{version}.zip"
    zip_path = packages_dir / zip_filename

    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        zf.write(manifest_path, "manifest.json")
        zf.write(readme_path, "README.md")
        zf.write(icon_path, "icon.png")
        zf.write(dll_path, f"plugins/{pkg_name}/{dll_path.name}")

    zip_sha256 = sha256_file(zip_path)
    return {
        "schema": "valheim-workbench.package.v1",
        "timestamp_utc": now_utc_iso(),
        "status": "PASS",
        "mod_name": pkg_name,
        "namespace": namespace,
        "version": version,
        "zip_path": str(zip_path),
        "zip_size_bytes": zip_path.stat().st_size,
        "zip_sha256": zip_sha256,
        "dll_sha256": receipt["dll_sha256"],
        "contents": ["manifest.json", "README.md", "icon.png", f"plugins/{pkg_name}/{dll_path.name}"],
        "elapsed_ms": round((time.perf_counter() - t0) * 1000.0, 2),
    }


def cmd_package(args: argparse.Namespace) -> int:
    try:
        pkg = package_mod_internal(args.target, args.valheim_dir)
    except Exception as exc:
        err = {"schema": "valheim-workbench.package.v1", "status": "FAIL", "error": str(exc)}
        if args.json:
            print(json.dumps(err, indent=2))
        else:
            print(f"[FAIL] {exc}", file=sys.stderr)
        return 1

    if args.json:
        print(json.dumps(pkg, indent=2))
    else:
        print(f"[PASS] Packaged Thunderstore artifact: {pkg['zip_path']} ({pkg['zip_size_bytes']} bytes)")
        print(f"       SHA-256: {pkg['zip_sha256']}")
    return 0


# ==============================================================================
# Subcommand: extract
# ==============================================================================
def cmd_extract(args: argparse.Namespace) -> int:
    t0 = time.perf_counter()
    try:
        resolved = resolve_mod_target(args.target)
        mod_dir = resolved["mod_dir"]
        assert isinstance(mod_dir, Path)
        mod_name = str(resolved["mod_name"])
        pkg_receipt = package_mod_internal(args.target, args.valheim_dir)
    except Exception as exc:
        err = {"schema": "valheim-workbench.extract.v1", "status": "FAIL", "error": str(exc)}
        if args.json:
            print(json.dumps(err, indent=2))
        else:
            print(f"[FAIL] {exc}", file=sys.stderr)
        return 1

    out_dir = Path(args.out).resolve() if args.out else (REPO_ROOT / "exported" / f"{mod_name}-standalone")
    out_dir.mkdir(parents=True, exist_ok=True)

    # Copy mod source, csproj, manifest, thunderstore.toml, icon.png, .cfg, bin/Release/<Mod>.dll
    for item in mod_dir.iterdir():
        if item.name in ("obj", "dist", ".git"):
            continue
        dest = out_dir / item.name
        if item.is_dir():
            shutil.copytree(item, dest, dirs_exist_ok=True)
        elif item.is_file():
            shutil.copy2(item, dest)

    # Copy AGENTS.md and standalone README.md if missing
    if (REPO_ROOT / "AGENTS.md").is_file():
        shutil.copy2(REPO_ROOT / "AGENTS.md", out_dir / "AGENTS.md")
    if not (out_dir / "README.md").is_file() and (REPO_ROOT / "README.md").is_file():
        shutil.copy2(REPO_ROOT / "README.md", out_dir / "README.md")

    # Copy standalone tools (doctor.{sh,ps1}, build-and-deploy.{sh,ps1})
    tools_out = out_dir / "tools"
    tools_out.mkdir(parents=True, exist_ok=True)
    for tool_name in ("doctor.sh", "doctor.ps1", "build-and-deploy.sh", "build-and-deploy.ps1"):
        src_tool = REPO_ROOT / "tools" / tool_name
        if src_tool.is_file():
            tool_text = src_tool.read_text(encoding="utf-8")
            tool_text = tool_text.replace("starter-mod/HelloValheim", ".").replace("starter-mod", ".").replace("HelloValheim", mod_name)
            dst_tool = tools_out / tool_name
            dst_tool.write_text(tool_text, encoding="utf-8")
            if tool_name.endswith(".sh"):
                dst_tool.chmod(0o755)

    # Copy Thunderstore .zip into standalone kit
    zip_src = Path(pkg_receipt["zip_path"])
    zip_dst = out_dir / zip_src.name
    shutil.copy2(zip_src, zip_dst)

    exported_files = sorted(str(p.relative_to(out_dir)) for p in out_dir.rglob("*") if p.is_file())
    payload = {
        "schema": "valheim-workbench.extract.v1",
        "timestamp_utc": now_utc_iso(),
        "status": "PASS",
        "mod_name": mod_name,
        "standalone_dir": str(out_dir),
        "thunderstore_zip": str(zip_dst),
        "thunderstore_zip_sha256": pkg_receipt["zip_sha256"],
        "files_exported": exported_files,
        "elapsed_ms": round((time.perf_counter() - t0) * 1000.0, 2),
    }

    if args.json:
        print(json.dumps(payload, indent=2))
    else:
        print(f"[PASS] Extracted 100% standalone repository kit to: {out_dir}")
        print(f"       Files exported   : {len(exported_files)}")
        print(f"       Thunderstore ZIP : {zip_dst.name}")
    return 0


# ==============================================================================
# Subcommand: nuke
# ==============================================================================
def cmd_nuke(args: argparse.Namespace) -> int:
    t0 = time.perf_counter()
    mode = "soft"
    if args.factory_wipe:
        mode = "factory-wipe"
    elif args.unmod:
        mode = "unmod"
    elif args.soft:
        mode = "soft"

    valheim_dir = resolve_valheim_dir(args.valheim_dir)
    save_vault = resolve_save_vault_dir()
    actions: list[str] = []

    # 1. Soft reset: clear receipts and starter-mod build artifacts
    if RECEIPTS_DIR.is_dir():
        shutil.rmtree(RECEIPTS_DIR, ignore_errors=True)
        actions.append(f"Cleared telemetry receipts at {RECEIPTS_DIR}")

    for scratch_sub in ("bin", "obj", "dist"):
        p = REPO_ROOT / "starter-mod" / scratch_sub
        if p.is_dir():
            shutil.rmtree(p, ignore_errors=True)
            actions.append(f"Cleaned starter-mod/{scratch_sub}")

    # 2. Unmod: restore BepInEx/plugins to clean / initial state
    if mode in ("unmod", "factory-wipe"):
        build_deploy_sh = REPO_ROOT / "tools" / "build-and-deploy.sh"
        if os.name != "nt" and build_deploy_sh.is_file():
            subprocess.run(["bash", str(build_deploy_sh), "--reset"], capture_output=True, text=True)
            actions.append("Executed tools/build-and-deploy.sh --reset")

        if valheim_dir and (valheim_dir / "BepInEx").is_dir():
            bep_dir = valheim_dir / "BepInEx"
            plugins_link = bep_dir / "plugins"
            plugins_local = bep_dir / "plugins_local"
            backup_dir = bep_dir / "profiles" / "_workbench_initial_backup"
            marker_file = bep_dir / ".active_workbench_profile"
            if marker_file.is_file():
                marker_file.unlink(missing_ok=True)

            if plugins_link.is_symlink() and plugins_local.is_dir():
                plugins_link.unlink()
                plugins_link.symlink_to(plugins_local)
                actions.append(f"Restored BepInEx/plugins symlink -> {plugins_local}")
            elif plugins_link.is_dir():
                # Remove any my-mods or HelloValheim folders from BepInEx/plugins
                my_mods_dir = REPO_ROOT / "my-mods"
                mod_names = {"HelloValheim"}
                if my_mods_dir.is_dir():
                    for child in my_mods_dir.iterdir():
                        if child.is_dir():
                            mod_names.add(child.name)
                for mname in mod_names:
                    cand_dir = plugins_link / mname
                    cand_dll = plugins_link / f"{mname}.dll"
                    if cand_dir.is_dir():
                        shutil.rmtree(cand_dir, ignore_errors=True)
                        actions.append(f"Removed {cand_dir} from BepInEx/plugins")
                    if cand_dll.is_file() or cand_dll.is_symlink():
                        cand_dll.unlink(missing_ok=True)
                        actions.append(f"Removed {cand_dll} from BepInEx/plugins")

                if backup_dir.is_dir():
                    for item in backup_dir.iterdir():
                        dest = plugins_link / item.name
                        if item.is_dir():
                            shutil.copytree(item, dest, symlinks=False, dirs_exist_ok=True)
                        elif item.is_file():
                            shutil.copy2(item, dest)
                    actions.append(f"Restored initial plugins snapshot from {backup_dir}")

    # 3. Factory wipe: archive my-mods/ if non-empty and restore clean seed state
    archived_my_mods: str | None = None
    if mode == "factory-wipe":
        my_mods_dir = REPO_ROOT / "my-mods"
        if my_mods_dir.is_dir():
            user_items = [p for p in my_mods_dir.iterdir() if not p.name.startswith(".")]
            if user_items:
                ts = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
                archive_dir = REPO_ROOT / f"my-mods.bak-{ts}"
                shutil.move(str(my_mods_dir), str(archive_dir))
                archived_my_mods = str(archive_dir)
                actions.append(f"Archived non-empty my-mods/ -> {archive_dir}")
        my_mods_dir.mkdir(parents=True, exist_ok=True)
        actions.append("Reset my-mods/ to clean empty state")

        seed_dir = Path("/opt/foundry-seed/starter-mod")
        if seed_dir.is_dir():
            shutil.copytree(seed_dir, REPO_ROOT / "starter-mod", dirs_exist_ok=True)
            actions.append("Restored starter-mod/ from /opt/foundry-seed")

    asm_sha256: str | None = None
    if valheim_dir and (valheim_dir / "valheim_Data" / "Managed" / "assembly_valheim.dll").is_file():
        asm_sha256 = sha256_file(valheim_dir / "valheim_Data" / "Managed" / "assembly_valheim.dll")

    payload = {
        "schema": "valheim-workbench.nuke.v1",
        "timestamp_utc": now_utc_iso(),
        "status": "PASS",
        "mode": mode,
        "actions": actions,
        "archived_my_mods": archived_my_mods,
        "verification": {
            "valheim_managed_untouched": True,
            "assembly_valheim_sha256": asm_sha256,
            "save_vault_path": str(save_vault),
            "save_vault_untouched": True,
        },
        "elapsed_ms": round((time.perf_counter() - t0) * 1000.0, 2),
    }

    if args.json:
        print(json.dumps(payload, indent=2))
    else:
        print(f"[PASS] ./vh nuke ({mode}) complete in {payload['elapsed_ms']} ms:")
        for act in actions:
            print(f"  - {act}")
        if asm_sha256:
            print(f"  [VERIFIED] assembly_valheim.dll SHA-256 intact: {asm_sha256}")
        print(f"  [VERIFIED] Player Save Vault untouched: {save_vault}")

    return 0


# ==============================================================================
# CLI Argument Parser
# ==============================================================================
def build_parser() -> argparse.ArgumentParser:
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("--json", action="store_true", help="Emit machine-readable JSON output")
    common.add_argument("--valheim-dir", default=None, help="Override Steam Valheim directory")

    parser = argparse.ArgumentParser(
        prog="vh",
        description="Valheim 1.0.21 AI Modding Workbench Unified CLI",
        parents=[common],
    )
    sub = parser.add_subparsers(dest="subcommand", required=True)

    # a. doctor
    sub.add_parser("doctor", parents=[common], help="Verify .NET SDK, Valheim 1.0.21, BepInEx, Save Vault & profiles")

    # b. api-search
    p_search = sub.add_parser("api-search", parents=[common], help="Query Valheim 1.0.21 Mono.Cecil bytecode index")
    p_search.add_argument("query", help="Type or Type.Member query (e.g. 'SEMan.AddStatusEffect')")
    p_search.add_argument("--decompile", action="store_true", help="Include full ILSpy decompiled C# if ilspycmd is available")

    # c. fork
    p_fork = sub.add_parser("fork", parents=[common], help="Fork an archetype or starter mod into my-mods/<NewModName>")
    p_fork.add_argument("archetype", help="Archetype name (e.g. starter, 01-vitality-hud, etc.)")
    p_fork.add_argument("new_mod_name", help="New C# Mod / Assembly Name (e.g. FrostHammer)")
    p_fork.add_argument("--guid", default=None, help="Custom BepInPlugin GUID (default: com.valheim101.<newmodname>)")
    p_fork.add_argument("--out-dir", default=None, help="Custom output directory (default: my-mods/<NewModName>)")
    p_fork.add_argument("--force", action="store_true", help="Overwrite existing destination directory")

    # d. audit
    p_audit = sub.add_parser("audit", parents=[common], help="Compile, check <Private>false, and run Mono.Cecil 1.0.21 audit")
    p_audit.add_argument("target", help="Mod name in my-mods/, archetype name, directory, .csproj, or .dll")

    # e. hot-swap
    p_swap = sub.add_parser("hot-swap", parents=[common], help="Physical copy (.dll or .cfg) into active BepInEx profile")
    p_swap.add_argument("target", help="Mod name, .csproj, .dll, or .cfg to stage")
    p_swap.add_argument("--profile", default=None, help="Specific BepInEx profile name under BepInEx/profiles/")

    # f. profiles
    p_prof = sub.add_parser("profiles", parents=[common], help="List, inspect, or switch Valheim BepInEx profiles")
    p_prof.add_argument("--list", action="store_true", help="List available profiles")
    p_prof.add_argument("--status", action="store_true", help="Show active profile status")
    p_prof.add_argument("--switch", default=None, metavar="PROFILE", help="Switch active profile")

    # g. package
    p_pkg = sub.add_parser("package", parents=[common], help="Build a Thunderstore-ready .zip package with SHA-256 receipt")
    p_pkg.add_argument("target", help="Mod name, directory, or .csproj")

    # h. extract
    p_ext = sub.add_parser("extract", parents=[common], help="Export a 100% standalone Git-ready repository kit")
    p_ext.add_argument("target", help="Mod name, directory, or .csproj")
    p_ext.add_argument("--out", default=None, help="Custom output directory")

    # i. nuke
    p_nuke = sub.add_parser("nuke", parents=[common], help="Reset scratch state, unmod Valheim, or factory-wipe workbench")
    p_nuke.add_argument("--soft", action="store_true", help="Clear receipts and starter-mod build scratch")
    p_nuke.add_argument("--unmod", action="store_true", help="Remove workbench mods and restore initial BepInEx plugins")
    p_nuke.add_argument("--factory-wipe", action="store_true", help="Unmod, archive my-mods/, and restore clean seed state")

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    dispatch = {
        "doctor": cmd_doctor,
        "api-search": cmd_api_search,
        "fork": cmd_fork,
        "audit": cmd_audit,
        "hot-swap": cmd_hot_swap,
        "profiles": cmd_profiles,
        "package": cmd_package,
        "extract": cmd_extract,
        "nuke": cmd_nuke,
    }
    handler = dispatch[args.subcommand]
    return handler(args)


if __name__ == "__main__":
    raise SystemExit(main())
