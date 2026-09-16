# Valheim Profile Engine: Operations & Use Cases Guide

This document is the definitive operational reference for managing Valheim profiles, mod manifests, dependencies, hooks, and configurations.

Every use case provides two distinct operational paths:
* **Path A: Without MCP** — Pure Command Line (PowerShell/Bash), declarative JSON manifests, and Git.
* **Path B: With MCP** — FastMCP Agent Tool Surface, JSON-RPC, and automated AI orchestration.

For setting up the MCP gateway locally or in Docker containers, see [Setting Up FastMCP](MCP_SETUP_LOCAL_AND_CONTAINER.md).

---

## 📑 Table of Contents

1. [CRUD for Profiles](#1-crud-for-profiles)
2. [CRUD for Mods, Manifests, Hooks & Timers](#2-crud-for-mods-manifests-hooks--timers)
3. [CRUD for Mod Configurations (.cfg)](#3-crud-for-mod-configurations-cfg)
4. [Activation & Swapping Workflows](#4-activation--swapping-workflows)
   - [Swapping a Profile](#41-swapping-a-profile)
   - [Swapping an Individual Mod](#42-swapping-an-individual-mod)
   - [Swapping a Manifest](#43-swapping-a-manifest)
   - [Swapping a Combination (Synthetic Overlays)](#44-swapping-a-combination-synthetic-overlays)
5. [Current State Visualizations](#5-current-state-visualizations)
   - [Terminal Text Tables](#51-terminal-text-tables)
   - [Local Web Dashboard on Isolate](#52-local-web-dashboard-on-isolate)
   - [Interactive Archify Diagrams](#53-interactive-archify-diagrams)
6. [Git Change History & Multi-Node Lifecycle](#6-git-change-history--multi-node-lifecycle)

---

## 1. CRUD for Profiles

A **Profile** represents a discrete mod environment (e.g. `full-gaming`, `sovereign-trio`, `isolated-unfaded`, `vanilla`).

### Create (New Profile)
* **Path A (Without MCP)**:
  1. Add a new profile definition to `manifests/profiles.json`:
     ```json
     "speed-testing": {
       "description": "Minimal benchmark harness",
       "alias": ["speed", "benchmark"],
       "target_dir": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Valheim\\BepInEx\\profiles\\speed-testing",
       "synthetic_links": []
     }
     ```
  2. Create the physical target folder on disk:
     ```powershell
     New-Item -ItemType Directory -Path "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\profiles\speed-testing"
     ```
  3. Copy or link your desired `.dll` files into that directory.

* **Path B (With MCP)**:
  Call the manifest management tool or execute via FastMCP prompt:
  ```python
  # Agent prompt: "Create a new profile named 'speed-testing' aliased to 'speed'"
  # Invokes tool:
  fleet_create_profile(
      name="speed-testing",
      aliases=["speed", "benchmark"],
      description="Minimal benchmark harness"
  )
  ```

---

### Read / Inspect (List Profiles & Current State)
* **Path A (Without MCP)**:
  ```powershell
  # List all profiles defined in the manifest
  powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -List

  # View currently active profile and loaded DLL breakdown
  powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Status
  ```

* **Path B (With MCP)**:
  ```python
  # FastMCP tool call
  status = fleet_node_status()
  inventory = fleet_mod_inventory()
  # Returns JSON containing active_profile, is_zero_copy_junction, latency_ms, and mod list
  ```

---

### Update (Modify Profile Contents or Synthetic Links)
* **Path A (Without MCP)**:
  - Add or remove `.dll` files in the profile's dedicated directory.
  - Or add synthetic compiler links to `manifests/profiles.json`:
    ```json
    "synthetic_links": [
      {
        "link_name": "Unfaded.dll",
        "source_path": "c:\\work\\deepnorthtesting\\plugins\\Unfaded\\bin\\Debug\\net48\\Unfaded.dll"
      }
    ]
    ```
  - Re-apply the profile:
    ```powershell
    powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Profile speed-testing -Force
    ```

* **Path B (With MCP)**:
  ```python
  fleet_update_profile(
      profile="speed-testing",
      synthetic_links=[{"link_name": "Unfaded.dll", "source_path": "..."}]
  )
  ```

---

### Delete (Remove Profile)
* **Path A (Without MCP)**:
  1. Ensure the profile is **not active** (switch away first):
     ```powershell
     powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Profile vanilla
     ```
  2. Delete the profile folder from `BepInEx/profiles/`:
     ```powershell
     Remove-Item -Recurse -Force "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\profiles\speed-testing"
     ```
  3. Remove the entry from `manifests/profiles.json`.

* **Path B (With MCP)**:
  ```python
  # Safety interlock automatically prevents deleting an active profile
  fleet_delete_profile(profile="speed-testing")
  ```

---

## 2. CRUD for Mods, Manifests, Hooks & Timers

Each mod assembly can expose metadata: dependencies, keybinds, Harmony patches (`on_run_hooks`), network RPC events, and update timers.

### Schema Definition
```json
{
  "name": "Unfaded.dll",
  "version": "1.0.2",
  "guid": "djcdevelopment.valheim.unfaded",
  "dependencies": [
    "denikson-BepInExPack_Valheim-5.4.2202"
  ],
  "exposed_actions": [
    { "key": "K", "action": "KillerCam", "default": "K", "description": "Locks camera onto killer upon death" },
    { "key": "F", "action": "FreeFly Drone", "default": "F", "description": "Enables free-cam spectator mode" },
    { "key": "Space", "action": "Manual Respawn", "default": "Space", "description": "Instant respawn override" },
    { "key": "F9", "action": "Screen Record", "default": "F9", "description": "Trigger cinematic capture" }
  ],
  "on_run_hooks": [
    { "target": "Hud.UpdateBlackScreen", "patch_type": "Prefix", "purpose": "Eliminates 9.5s fade delay" },
    { "target": "Player.OnDeath", "patch_type": "Postfix", "purpose": "Initializes spectator rig" },
    { "target": "GameCamera.UpdateCamera", "patch_type": "Postfix", "purpose": "Applies custom pitch/yaw" }
  ],
  "network_events": [
    { "rpc": "Unfaded_BroadcastSpectator", "direction": "ClientToServer" }
  ],
  "timers": [
    { "name": "SpectatorPoll", "interval_ms": 100, "handler": "TickSpectatorCamera" }
  ]
}
```

### Create / Register Mod Metadata
* **Path A (Without MCP)**:
  Add the mod entry to `manifests/profiles.json` under `mod_metadata` or your mod repository's `manifest.json`.
* **Path B (With MCP)**:
  Add or update `KNOWN_MOD_METADATA` in `c:\work\isolate\network\mcp\comfy_gateway\toolsurface\fleet.py`.

---

### Read / Audit Conflicts
* **Path A (Without MCP)**:
  ```powershell
  # Run the pre-flight conflict audit script
  python -c "from comfy_gateway.toolsurface.fleet import fleet_conflict_audit; import pprint; pprint.pprint(fleet_conflict_audit('OMEN'))"
  ```
* **Path B (With MCP)**:
  ```python
  # FastMCP tool call
  audit = fleet_conflict_audit(node="OMEN")
  # Returns:
  # {
  #   "has_keybind_conflicts": False,
  #   "keybind_conflicts": [],
  #   "has_hook_overlaps": False,
  #   "active_keybinds": { "K": [...], "F7": [...], "V": [...] }
  # }
  ```

---

### Disable / Delete Mod
* **Path A (Without MCP)**:
  ```powershell
  # Temporary disable: rename to .disabled (BepInEx ignores non-.dll files)
  Rename-Item "BepInEx\plugins\Unfaded.dll" "Unfaded.dll.disabled"

  # Complete removal: delete file from profile directory
  Remove-Item "BepInEx\profiles\full-gaming\Unfaded.dll"
  ```
* **Path B (With MCP)**:
  ```python
  fleet_toggle_mod(mod="Unfaded.dll", enabled=False, machine="OMEN")
  ```

---

## 3. CRUD for Mod Configurations (.cfg)

BepInEx stores configurations in `BepInEx/config/<ModGUID>.cfg`.

### Create (Generate Configuration)
* **Path A (Without MCP)**:
  Launch the game once with the mod loaded. BepInEx automatically generates the default `.cfg` file with commented sections.
* **Path B (With MCP)**:
  Call configuration template generator to generate `.cfg` before launching.

---

### Read / Inspect Configuration
* **Path A (Without MCP)**:
  - **In-Game**: Press **`F1`** to open BepInEx `ConfigurationManager` graphical editor.
  - **Text Editor**: Open `BepInEx/config/djcdevelopment.valheim.unfaded.cfg`.
* **Path B (With MCP)**:
  ```python
  cfg = valheim_read_config("djcdevelopment.valheim.unfaded.cfg")
  print(cfg["General"]["KillerCamHotkey"])
  ```

---

### Update Configuration
* **Path A (Without MCP)**:
  Modify values in the `.cfg` file directly:
  ```ini
  [General]
  ## Hotkey to focus killer camera
  # Setting type: KeyboardShortcut
  # Default value: K
  KillerCamHotkey = K
  ```
* **Path B (With MCP)**:
  ```python
  valheim_update_config(
      cfg_file="djcdevelopment.valheim.unfaded.cfg",
      section="General",
      key="KillerCamHotkey",
      value="J"
  )
  ```

---

### Delete / Reset Configuration
* **Path A (Without MCP)**:
  Delete the `.cfg` file. BepInEx regenerates default values on next launch:
  ```powershell
  Remove-Item "BepInEx\config\djcdevelopment.valheim.unfaded.cfg"
  ```
* **Path B (With MCP)**:
  ```python
  valheim_reset_config("djcdevelopment.valheim.unfaded.cfg")
  ```

---

## 4. Activation & Swapping Workflows

### 4.1 Swapping a Profile
Switch between entire mod environments in under 50ms without copying files:

* **Path A (Without MCP)**:
  ```powershell
  # Switch to Sovereign Trio
  powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Profile sovereign-trio

  # Switch to Full 65-Mod Gaming Suite
  powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Profile full-gaming
  ```

* **Path B (With MCP)**:
  ```python
  fleet_swap_profile(profile="sovereign-trio", machine="OMEN")
  ```

---

### 4.2 Swapping an Individual Mod
Swap between release build and debug build of a specific mod without changing other mods:

* **Path A (Without MCP)**:
  Use an NTFS Hardlink (`mklink /H`):
  ```powershell
  # Replace existing DLL with hardlink to compiler debug output
  Remove-Item "BepInEx\plugins\Unfaded.dll"
  cmd /c mklink /H "BepInEx\plugins\Unfaded.dll" "c:\work\deepnorthtesting\plugins\Unfaded\bin\Debug\net48\Unfaded.dll"
  ```

* **Path B (With MCP)**:
  ```python
  fleet_link_mod_build(
      mod="Unfaded.dll",
      build_target="c:\\work\\deepnorthtesting\\plugins\\Unfaded\\bin\\Debug\\net48\\Unfaded.dll"
  )
  ```

---

### 4.3 Swapping a Manifest
Switch between different manifest sets (e.g. testing against local server vs remote dedicated server):

* **Path A (Without MCP)**:
  ```powershell
  powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Manifest manifests\profiles-am4-server.json -Profile server-test
  ```

* **Path B (With MCP)**:
  ```python
  fleet_load_manifest(manifest_path="manifests/profiles-am4-server.json")
  ```

---

### 4.4 Swapping a Combination (Synthetic Overlays)
A **Synthetic Profile** allows you to test live mod builds while keeping dependencies in place:
1. Base dependencies reside in `BepInEx/profiles/synthetic/` (e.g. `BepInEx.dll`, `HookGenPatcher.dll`).
2. Directory junctions inside `synthetic/` point straight to your IDE build output (`bin/Release` or `bin/Debug`).
3. Recompiling in Visual Studio or VS Code immediately updates the game's active plugins with **zero file copies**.

---

## 5. Current State Visualizations

### 5.1 Terminal Text Tables
Run directly from PowerShell for instant inspection:

```powershell
powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Status
```

Output:
```text
======================================================
  Deep North Testing :: BepInEx Profile State (OMEN)
======================================================
 Plugins Path   : C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins
 Is Zero-Copy   : YES (NTFS Junction)
 Junction Target: C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\profiles\full-comfymods
 Active Profile : full-gaming
 Active DLLs    : 65 assemblies loaded
```

Run the automated integrity suite:
```powershell
powershell -ExecutionPolicy Bypass -File tools\Verify-ProfileState.ps1
```

---

### 5.2 Local Web Dashboard on Isolate
The repository includes a lightweight, zero-dependency Python dashboard server in `tools/serve-dashboard.py`.

#### Launching the Dashboard:
```powershell
python tools\serve-dashboard.py
```

#### What It Provides:
* Accessible locally at **`http://localhost:8725`** and across Tailscale at **`http://100.116.82.60:8725`**.
* **Fleet Node Status Cards**: Real-time ping, online/offline state, and active profiles for **OMEN**, **AM4**, **FX99**, and **i5**.
* **1-Click Profile Swapping**: Dropdown and button triggering zero-copy swaps via backend API.
* **Live Conflict Radar Matrix**: Visual badges displaying keybind conflicts and Harmony hook collisions.
* **Direct Links**: Embedded navigation to the Archify interactive viewers.

---

### 5.3 Interactive Archify Diagrams
Standalone interactive SVG/HTML diagrams rendered in dark and light mode:

* [**Chapter 1: Junction Swap**](diagram-1-junction-swap.html) — Explores the NTFS Reparse Point pointer redirect mechanics.
* [**Chapter 2: Sovereign Mod Matrix**](diagram-2-sovereign-matrix.html) — Visualizes the 3-mod isolation layer (`IsModded`, `Unfaded`, `TotemSentinel`).
* [**Chapter 3: Synthetic Pipeline**](diagram-3-synthetic-pipeline.html) — Visualizes the zero-deploy compiler build loop.
* [**Chapter 4: Fleet Conflict Radar**](diagram-4-fleet-conflict.html) — Topology of OMEN, AM4, FX99, and i5 conflict scanning.
* [**Macro Compendium**](valheim-profile-engine.html) — Full unified system topology.

---

## 6. Git Change History & Multi-Node Lifecycle

### Recommended Git Repository Structure
```text
valheim-profile-engine/
├── manifests/
│   └── profiles.json              <-- TRACKED: Single source of truth for profiles
├── tools/
│   ├── switch-profile.ps1         <-- TRACKED: Reparse switcher engine
│   ├── Verify-ProfileState.ps1    <-- TRACKED: Automated verification suite
│   └── serve-dashboard.py        <-- TRACKED: Web dashboard server
├── docs/                          <-- TRACKED: Architecture, guides, infographics
└── .gitignore                     <-- TRACKED: Excludes binaries and ephemeral logs
```

### Git Policy (`.gitignore`)
```gitignore
# Never commit compiled binaries or Unity symbols
*.dll
*.pdb
*.mdb

# Never commit active junction link target
BepInEx/plugins

# Never commit runtime logs and telemetry
BepInEx/LogOutput.log
Player.log
network/mcp/var/fleet_cache.json
```

### Tracking Configuration Changes
```powershell
# 1. View changes made to profile catalog
git diff manifests/profiles.json

# 2. Track keybind changes across versions
git log -p -S "KillerCamHotkey" manifests/profiles.json

# 3. Commit profile updates
git commit -m "feat(profiles): add sovereign-trio profile with zero-copy compiler links"
```

### Multi-Node Git Sync Across the Fleet
When a profile is added or modified on OMEN:
1. Push the commit to the central repo:
   ```powershell
   git push origin main
   ```
2. On remote nodes (e.g. AM4 dedicated server or FX99):
   ```bash
   ssh am4 "cd /home/derek/valheim-profile-engine && git pull"
   ```
3. Remote nodes now have access to the exact same profile manifest without copying large mod packs across the network.
