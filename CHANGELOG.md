# Changelog

All notable changes to the **Valheim Profile Engine** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-09-16

### 🚀 Added
- **Sub-50ms Zero-Copy Engine**: Native Windows NTFS Directory Junction (`mklink /J`) switching via `tools/switch-profile.ps1`.
  - Swaps 65-mod gaming suites in **39ms - 72ms** (182x faster than copying 85MB).
  - 0 bytes written to disk, eliminating SSD write wear and IOPS bottlenecks.
  - 100% unprivileged execution (requires zero Administrator UAC elevation).
- **Synthetic Compiler Linking Pipeline**: Direct hardlink/junction overlays connecting C# IDE build outputs (`bin/Release` / `bin/Debug`) directly to `BepInEx/plugins`.
  - Build once in Visual Studio / JetBrains Rider—changes are live immediately in-game.
- **Safety Interlock System**:
  - Running process lock detection (`Get-Process valheim*`) preventing filesystem changes while the game is active.
  - Automatic physical backup (`BepInEx/profiles/backup-before-junction`) prior to first-time junction conversion.
  - Safe unlinking (`rmdir` removes only the reparse point, preserving all target files).
- **Automated Verification Suite (`tools/Verify-ProfileState.ps1`)**:
  - Phase 1: Manifest JSON schema validation.
  - Phase 2: NTFS Reparse Point attribute audit (`IO_REPARSE_TAG_MOUNT_POINT`).
  - Phase 3: Active assembly inventory and sovereign module detection.
  - Phase 4: 100-pass directory traversal latency benchmark (<0.05ms overhead).
- **Developer Fleet Mod Matrix Dashboard (`tools/serve-dashboard.py`)**:
  - Live on `http://localhost:8725`.
  - Sticky header bar displaying node latency pills (`Primary Rig`, `Dedicated Server`, `Linux Client`, `Test Laptop`).
  - 4 side-by-side columns tracking currently and last-known running mods.
  - Real-time interactive text filtering across all machines simultaneously.
- **FastMCP Fleet Tool Surface**:
  - `fleet_mod_inventory()`: Multi-node live and cached mod inventory discovery.
  - `fleet_mod_matrix()`: Formatted multi-machine comparison table (Markdown / Text / JSON).
  - `fleet_conflict_audit()`: Pre-flight hotkey collision and Harmony hook overlap detector.
  - `fleet_swap_profile()`: Programmatic profile switching for autonomous AI orchestrators.
- **Comprehensive Documentation Suite**:
  - `README.md`: Executive showcase, 5-chapter technical deep dive, benchmarks, and interactive macro compendium.
  - `docs/OPERATIONS_AND_USE_CASES.md`: Dual-path operational playbook (CLI vs FastMCP) covering full profile, mod, and config CRUD.
  - `docs/MCP_SETUP_LOCAL_AND_CONTAINER.md`: Local Python venv and Docker Compose deployment guides.
  - `docs/FAQ_AND_TROUBLESHOOTING.md`: Technical answers to filesystem mechanics, Linux/Steam Deck usage, and update behaviors.
  - `CONTRIBUTING.md`: Architectural principles, manifest templates, and PR guidelines.
- **Visual Artifacts & Infographics**:
  - 5 NotebookLM-optimized infographic source specifications (`docs/infographics/`).
  - 6 OpenArt / Flux concept art generation prompts.
  - 11 embedded visual assets (`docs/assets/`).
  - 5 standalone interactive Archify architecture viewers (`docs/*.html`).

### 🛡️ Security & Privacy
- Full sanitization audit removing all private machine hostnames, usernames, and private network IPs.
- Dynamic error message redaction preventing remote hostname leakage in SSH timeout traces.
- Robust `.gitignore` configuration preventing byte-compiled caches or runtime telemetry from being tracked.

### 🙏 Acknowledgments
- **TylerS76** (Discord `@TylerS76`): The foundational architectural spark proposing filesystem linking to eliminate mod file copy overhead.
- **durracktu / djcdevelopment**: Engine architecture, declarative catalog, synthetic build pipeline, and FastMCP integration.
- **BepInEx Team**: Valheim modding foundation and runtime assembly injection.
