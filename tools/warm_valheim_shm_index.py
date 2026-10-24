#!/usr/bin/env python3
"""
warm_valheim_shm_index.py — Pre-warms `/dev/shm/valheim-1.0.20-index` and `/dev/shm/mechsuit-ast-arena`
with Valheim 1.0.20 Mono.Cecil bytecode stubs, DeepNorthTesting plugins, and the Turn-1 Shared Context Pack.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from pathlib import Path

FLASH_ROOT = Path("/home/derek/work/commandcenter-linux-flash")
if str(FLASH_ROOT) not in sys.path:
    sys.path.insert(0, str(FLASH_ROOT))

from hearth.delivery import sourcemap

DEEPNORTH_ROOT = Path("/home/derek/work/deepnorthtesting")
MOD101_ROOT = Path("/home/derek/work/valheim-modding-101")
SHM_INDEX_DIR = Path("/dev/shm/valheim-1.0.20-index")
INSPECTOR_BIN = DEEPNORTH_ROOT / "tools" / "Inspector" / "bin" / "Release" / "net8.0" / "Inspector"
DOTNET_ROOT = Path.home() / ".local" / "share" / "discengine-dotnet"


TURN1_SHARED_PREAMBLE = """# VALHEIM 1.0.20 (DEEP NORTH) GROUND-TRUTH ENGINEERING CONTRACT & SHARED CONTEXT PACK

You are a Valheim 1.0.20 (*Deep North*, BepInEx `5.4.2202`, HarmonyX `2.9.0+`, `.NET Framework 4.8` / `net48`) systems engineer working inside the `deepnorthtesting` and `valheim-modding-101` repositories.

## 1. Non-Negotiable Valheim 1.0.20 Engineering Rules
1. **Target Framework & Zero DLL Leaks:** Always target `<TargetFramework>net48</TargetFramework>` and `<LangVersion>12</LangVersion>`. Every `<Reference>` to `assembly_*.dll`, `UnityEngine*.dll`, `BepInEx.dll`, and `0Harmony.dll` MUST have `<Private>false</Private>`.
2. **The 6 Breaking Valheim 1.0.20 API Signatures (Never Use 0.217/0.218 Signatures):**
   - **Spatial Sectors (`ZDOMan`):** `ZDOMan.FindSectorObjects(Vector2s sector, SimulationDistance simulationDistance, List<ZDO> sectorObjects, List<ZDO> distantSectorObjects)` (`Vector2s` is in `assembly_utils.dll`, replacing `Vector2i` + `int area, int distantArea`).
   - **Status Effects (`SEMan`):** `SEMan.AddStatusEffect(int nameHash, bool resetTime, int itemLevel, float skillLevel, short variant)` — **5 parameters** (`typeof(short)` 5th parameter `variant` is mandatory when patching with `[HarmonyPatch]`). Also `SEMan.AddStatusEffect(StatusEffect statusEffect, bool resetTime, int itemLevel, float skillLevel, short variant)`.
   - **Overloaded Methods (`PieceTable` / `Character.Message` / `SEMan`):** Always disambiguate overloaded methods in `[HarmonyPatch]` with an explicit `new Type[] { ... }` parameter array (e.g., `[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.SetCategory), new Type[] { typeof(int) })]`, `Character.Message(MessageHud.MessageType type, string msg, int amount, Sprite icon, bool center)` — 5 parameters in 1.0.20) to prevent `AmbiguousMatchException`.
   - **Compiler Closures (`Chat`):** Modern C# lowers inner callbacks to local functions (`<OnNewChatMessage>g__OnCheckPermissionAsyncCompleted|0`). Use fallback reflection in `[HarmonyTargetMethod]` when targeting compiler-generated methods.
   - **UI & Input (`InventoryGui`):** `InventoryGui.Show(Container container, int activeGroup = 1)` takes **2 arguments** (`Container, int`). `InventoryGui.UseButtonHeld()` can be patched directly via Postfix. Always call `.Start()` before each `CodeMatcher` match.
   - **Early Save Path (`FileHelpers` / `Utils`):** Never call `Utils.GetSaveDataPath()` inside `BaseUnityPlugin.Awake()` (`PlatformManager.DistributionPlatform` is `null` during `Awake()`). Use `BepInEx.Paths.ConfigPath` or guard with `try/catch`.
3. **Runtime & Multiplayer Safety Guards:**
   - Always null-guard `Player.m_localPlayer` (`if (Player.m_localPlayer == null) return;`) and singleton instances (`ObjectDB.instance`, `ZNet.instance`, `Hud.instance`, `InventoryGui.instance`).
   - Protect headless Dedicated Servers (`if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;`) before touching HUD, camera, audio, or local input.
   - Wrap risky patches in `[HarmonyFinalizer]` so exceptions log once without breaking the player's frame loop.

## 2. Valheim Modding 101 Guide & Workbench Cross-Reference Map (For Community Replies)
- **Live Interactive Web Workbench:** `https://delightful-sand-0451e171e.1.azurestaticapps.net/`
- **GitHub Starter Kit Repo:** `https://github.com/djcdevelopment/valheim-modding-101`
- **Chamber Map for Contextual Deep-Linking:**
  - **Chamber 0:** The 3-Bucket Mod Taxonomy (Client QoL vs. Server-Synced C# vs. Unity AssetBundles) & Save/Achievement Safety.
  - **Chamber 1:** 30-Second Preflight Check (`tools/doctor.ps1` & `tools/doctor.sh` — fixes Steam Play #747 & Proton `WINEDLLOVERRIDES`).
  - **Chamber 2 & 3:** `<Private>false</Private>` `.csproj` setup, 1.2s compile, and `F5` console `vikingping` verification.
  - **Chamber 3.5:** Decoding `DMD<Class::Method>` Harmony stack traces in `LogOutput.log`, `Player.m_localPlayer == null` guards, and `[HarmonyFinalizer]`.
  - **Chamber 4:** Harmony Prefix/Postfix/Finalizer state passing (`struct State`) & the 6 Valheim 1.0.20 API breaking shifts.
  - **Chamber 4.5:** `Config.Bind` with `AcceptableValueRange` + `FileSystemWatcher` live `.cfg` hot-reload without restarting Valheim.
  - **Chamber 5:** Multiplayer `ZNetView.IsOwner()`, `ZRoutedRpc`, `ZNet.instance.IsDedicated()` guards, ILSpy inspection, and Thunderstore packaging.
"""


def main() -> int:
    t0 = time.perf_counter()
    env = os.environ.copy()
    env["DOTNET_ROOT"] = str(DOTNET_ROOT)
    env["PATH"] = f"{DOTNET_ROOT}:{env.get('PATH', '')}"

    if not (SHM_INDEX_DIR / "catalog.json").is_file():
        subprocess.run(
            [str(INSPECTOR_BIN), "--dump-index", str(SHM_INDEX_DIR)],
            env=env,
            check=True,
        )

    preamble_path = SHM_INDEX_DIR / "turn1_shared_preamble.md"
    preamble_path.write_text(TURN1_SHARED_PREAMBLE.strip() + "\n", encoding="utf-8")

    # Warm `/dev/shm/mechsuit-ast-arena` for all dumped Valheim 1.0.20 C# stubs + starter/deepnorth plugins
    classes_dir = SHM_INDEX_DIR / "classes"
    cs_files = sorted(classes_dir.glob("*.cs"))
    plugin_cs_files = sorted(DEEPNORTH_ROOT.glob("plugins/**/*.cs")) + sorted(MOD101_ROOT.glob("starter-mod/*.cs"))

    warmed_count = 0
    hits_count = 0
    for p in cs_files + plugin_cs_files:
        data = p.read_bytes()
        if len(data) > sourcemap.FILE_CAP_BYTES:
            continue
        fm = sourcemap.file_map(str(p.name), data)
        _, _, _, _, was_hit = sourcemap._shm_get_or_build_file_ast(fm)
        warmed_count += 1
        if was_hit:
            hits_count += 1

    elapsed_ms = round((time.perf_counter() - t0) * 1000.0, 2)
    summary = {
        "schema": "valheim-shm-index-warm.v1",
        "shm_index_dir": str(SHM_INDEX_DIR),
        "shm_ast_arena_dir": "/dev/shm/mechsuit-ast-arena",
        "cs_files_indexed": warmed_count,
        "arena_existing_hits": hits_count,
        "turn1_preamble_path": str(preamble_path),
        "turn1_preamble_bytes": preamble_path.stat().st_size,
        "elapsed_ms": elapsed_ms,
    }
    (SHM_INDEX_DIR / "warm_receipt.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
