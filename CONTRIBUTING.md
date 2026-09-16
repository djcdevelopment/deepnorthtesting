# Contributing to the Valheim Profile Engine

Thank you for your interest in improving the **Valheim Profile Engine**! Whether you are adding a new profile preset, documenting Harmony hook conflicts, optimizing PowerShell switching routines, or expanding our FastMCP tool surface, your contributions are warmly welcomed.

---

## 🏛️ Core Architectural Principles

When contributing code or manifests to this project, please adhere to our foundational design tenets:

1. **Zero-Copy & Non-Destructive**: Never copy, rewrite, or delete mod files when switching environments. Use native NTFS Directory Junctions (`mklink /J`) or hardlinks (`mklink /H`).
2. **Zero Administrator Elevation**: All user-facing tools must execute cleanly under standard Windows user permissions without UAC elevation or Developer Mode requirements.
3. **Safety First**: Respect process locks. Never modify or repoint `BepInEx/plugins` while `valheim.exe` or `valheim_server.exe` is actively running.
4. **Privacy by Default**: Never commit personal machine hostnames, private local usernames (`C:\Users\<user>`), or private network IPs. Use role labels (`Primary Rig`, `Dedicated Server`) and environment overrides.
5. **Honor Community Sparks**: Acknowledge community contributors and ideators who shape the architecture (e.g. TylerS76's filesystem linking breakthrough).

---

## 🛠️ How to Contribute

### 1. Adding or Updating Profiles
Profile presets are declared in [`manifests/profiles.json`](manifests/profiles.json).

#### Directory Profile Template:
```json
"my-modpack": {
  "alias": ["pack", "cinematic"],
  "description": "Clean camera and lighting suite for high-resolution photography",
  "type": "directory",
  "path": "C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/profiles/my-modpack"
}
```

#### Synthetic Profile Template (Compiler-to-Game Linking):
```json
"dev-testing": {
  "alias": ["dev", "test"],
  "description": "Direct compiler link to local C# build artifacts",
  "type": "synthetic",
  "target_dir": "C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/profiles/dev-testing",
  "entries": [
    {
      "name": "MyMod.dll",
      "source": "C:/work/MyMod/bin/Release/net48/MyMod.dll",
      "link_type": "hardlink"
    }
  ]
}
```

### 2. Registering Mod Conflict Metadata
Pre-flight conflict detection requires metadata on hotkeys and Harmony patches. When adding support for a new mod, document:
- Mod title, description, and author
- Registered hotkeys (default bindings and action descriptions)
- Harmony hook injection targets (e.g., `Hud.Update`, `GameCamera.UpdateCamera`)
- Sovereign/system tags (`camera`, `achievements`, `combat`, `utility`)

### 3. Tooling Guidelines
* **PowerShell (`tools/*.ps1`)**:
  - Must remain compatible with Windows PowerShell 5.1 and PowerShell 7+ (Core).
  - Use native Windows `cmd /c mklink` and `cmd /c rmdir` for reparse point manipulations.
  - Return explicit integer exit codes (`exit 0` on success, `exit 1` on fatal errors).
* **Python Tooling (`tools/*.py`)**:
  - Python 3.10+ with type hints (`from __future__ import annotations`).
  - Keep dashboard and CLI utilities zero-dependency where possible (using standard library `http.server`, `urllib`, `pathlib`, `json`).

---

## 🧪 Verification Checklist

Before submitting a Pull Request, run the local verification suite:

```powershell
# 1. Test status inspection
powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Status

# 2. Run the automated integrity suite (all 4 phases must pass)
powershell -ExecutionPolicy Bypass -File tools\Verify-ProfileState.ps1

# 3. Verify clean privacy audit (0 matches for personal data)
git grep -i "derek"
git grep -E "100\.[0-9]+\.[0-9]+\.[0-9]+"
```

---

## 📦 Pull Request Process

1. **Fork** the repository on GitHub.
2. **Create a branch** for your feature or fix:
   ```bash
   git checkout -b feat/add-cinematic-profile
   ```
3. **Commit** using Conventional Commits format:
   - `feat:` for new capabilities, profiles, or tools
   - `fix:` for bug fixes or path resolutions
   - `docs:` for documentation, diagrams, or visual specifications
   - `perf:` for latency or filesystem optimizations
   - `chore:` for repository maintenance or sanitization
4. **Push** to your fork and submit a **Pull Request** against `main`.
5. Clearly describe the problem solved, your testing environment, and verification results.
